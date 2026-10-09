using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// Loopback-only TLS transport fixture. Its private key/certificate never leave the test process.
sealed class HttpsFixture : IDisposable
{
    readonly TcpListener listener = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource stop = new();
    readonly X509Certificate2 certificate;
    readonly byte[] body;
    readonly int delay;
    readonly bool truncate;
    readonly Task server;
    public HttpsFixture(byte[] body, int delay = 0, bool truncate = false)
    {
        this.body = body; this.delay = delay; this.truncate = truncate;
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=github.com", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        listener.Start(); server = Serve();
    }
    public HttpMessageHandler Handler()
    {
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false,
            ConnectCallback = async (_, token) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(endpoint, token); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); throw; }
            },
            SslOptions = new SslClientAuthenticationOptions
            {
                // Pin the exact ephemeral fixture certificate; no trust relaxation enters production code.
                RemoteCertificateValidationCallback = (_, cert, _, _) => cert is not null && cert.GetCertHashString() == certificate.GetCertHashString()
            }
        };
    }
    async Task Serve()
    {
        try
        {
            using var tcp = await listener.AcceptTcpClientAsync(stop.Token);
            using var tls = new SslStream(tcp.GetStream(), false);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, stop.Token);
            var header = new List<byte>(); var one = new byte[1];
            while (header.Count < 16384 && await tls.ReadAsync(one, stop.Token) > 0)
            { header.Add(one[0]); if (header.Count >= 4 && System.Text.Encoding.ASCII.GetString(header.TakeLast(4).ToArray()) == "\r\n\r\n") break; }
            await tls.WriteAsync(System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"), stop.Token);
            int limit = truncate ? body.Length / 2 : body.Length;
            for (int i = 0; i < limit; i += 16384)
            { await tls.WriteAsync(body.AsMemory(i, Math.Min(16384, limit - i)), stop.Token); await tls.FlushAsync(stop.Token); if (delay > 0) await Task.Delay(delay, stop.Token); }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or SocketException) { }
    }
    public void Dispose()
    { stop.Cancel(); listener.Stop(); try { server.GetAwaiter().GetResult(); } catch (ObjectDisposedException) { } certificate.Dispose(); stop.Dispose(); }
}
