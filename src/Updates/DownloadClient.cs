using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace EvidenceHub.Updates;
public record TransferProgress(string Component, long Bytes, long Total, string Phase);
public sealed class DownloadClient : IDisposable
{
    readonly HttpClient http;
    public DownloadClient(HttpMessageHandler? handler = null)
    {
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false });
        http.Timeout = Timeout.InfiniteTimeSpan;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("EvidenceNeuterHub/0.2.0");
    }
    public void Dispose() => http.Dispose();
    async Task<HttpResponseMessage> Open(Uri uri, CancellationToken token)
    {
        for (int i = 0; i < 6; i++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0 || !(uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com")) throw new IOException("Download redirect was rejected.");
            var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                var location = response.Headers.Location; response.Dispose();
                if (location is null) throw new IOException("Download redirect has no destination.");
                uri = new Uri(uri, location); continue;
            }
            if (response.StatusCode == HttpStatusCode.NotFound) { response.Dispose(); throw new IOException("No public Genesis release is available yet. Your installed game remains playable offline."); }
            try { response.EnsureSuccessStatusCode(); return response; } catch { response.Dispose(); throw; }
        }
        throw new IOException("Too many download redirects.");
    }
    public async Task<string> MetadataAsync(CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(2));
        using var response = await Open(new Uri(Metadata.CatalogUrl), deadline.Token);
        using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int n;
        while ((n = await stream.ReadAsync(buffer, deadline.Token)) != 0)
        {
            if (output.Length + n > Metadata.MaxMetadataBytes) throw new IOException("Release metadata exceeded its size limit.");
            output.Write(buffer, 0, n);
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }
    public async Task DownloadAsync(ReleasePackage package, string destination, IProgress<TransferProgress>? progress, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromHours(2));
        var uri = Metadata.PackageUrl(package.Url);
        using var response = await Open(uri, deadline.Token);
        if (response.Content.Headers.ContentLength is long size && size != package.Size) throw new IOException("Package length differs from signed metadata.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var partial = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            await using (var stream = await response.Content.ReadAsStreamAsync(deadline.Token))
            {
                var buffer = new byte[65536]; long count = 0; int n; var tick = System.Diagnostics.Stopwatch.StartNew();
                while ((n = await stream.ReadAsync(buffer, deadline.Token)) != 0)
                {
                    count += n; if (count > package.Size) throw new IOException("Package exceeded its signed size.");
                    hash.AppendData(buffer, 0, n); await file.WriteAsync(buffer.AsMemory(0, n), deadline.Token);
                    if (tick.ElapsedMilliseconds >= 100 || count == package.Size) { progress?.Report(new(package.Kind, count, package.Size, "Downloading")); tick.Restart(); }
                }
                if (count != package.Size || Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() != package.Sha256) throw new IOException("Package integrity verification failed.");
                await file.FlushAsync(deadline.Token); file.Flush(true);
            }
            token.ThrowIfCancellationRequested(); File.Move(partial, destination, true);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
