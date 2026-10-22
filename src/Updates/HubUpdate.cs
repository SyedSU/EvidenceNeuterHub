using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EvidenceHub.Updates;
public record HubInstaller(string Url, long Size, string Sha256, string PublisherThumbprint);
public record HubManifest(int Schema, string Product, string Channel, string Platform, string Version, long Sequence,
    DateTimeOffset Expires, string SourceCommit, string MinimumHubVersion, int MinimumWindowsBuild, HubInstaller Installer);
public static class HubUpdate
{
    public const string ManifestUrl = "https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/hub-stable/hub-stable.json";
    public static string PublicKey() => Resource("EvidenceHub.HubReleaseKey");
    public static string Publisher() => Resource("EvidenceHub.HubPublisher").Trim();
    static string Resource(string name) { using var s=typeof(HubUpdate).Assembly.GetManifestResourceStream(name); return s is null ? "" : new StreamReader(s).ReadToEnd(); }
    public static HubManifest Verify(string envelope, string key, string publisher, long highestSequence, int windowsBuild)
    {
        if (string.IsNullOrWhiteSpace(key) || !Regex.IsMatch(publisher,"^[A-F0-9]{64}$")) throw new IOException("Hub update trust is not configured. Obtain a trusted installer from the publisher; no update was downloaded.");
        if (System.Text.Encoding.UTF8.GetByteCount(envelope)>Metadata.MaxMetadataBytes) throw new IOException("Hub metadata too large.");
        var signed=JsonSerializer.Deserialize<SignedRelease>(envelope,Metadata.Json) ?? throw new IOException("Empty Hub metadata.");
        var data=Convert.FromBase64String(signed.Payload);
        using var rsa=RSA.Create();rsa.ImportFromPem(key);
        if(rsa.KeySize<3072 || !rsa.VerifyData(data,Convert.FromBase64String(signed.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss)) throw new IOException("Hub signature invalid.");
        var m=JsonSerializer.Deserialize<HubManifest>(data,Metadata.Json) ?? throw new IOException("Empty Hub payload.");
        if(m.Schema!=1 || m.Product!="hub" || m.Channel!="stable" || m.Platform!="windows-x64" || m.Sequence<=highestSequence || m.Sequence<1 || m.Expires<=DateTimeOffset.UtcNow || !Regex.IsMatch(m.SourceCommit ?? "","^[a-f0-9]{40}$")) throw new IOException("Hub metadata unsupported, expired or replayed.");
        if(Metadata.VersionOf(m.Version)<=Metadata.VersionOf(Metadata.HubVersion) || Metadata.VersionOf(m.MinimumHubVersion)>Metadata.VersionOf(Metadata.HubVersion) || m.MinimumWindowsBuild<19041 || windowsBuild<m.MinimumWindowsBuild) throw new IOException("Hub update incompatible with this Hub or Windows. Windows 10 build "+m.MinimumWindowsBuild+" or newer is required.");
        if(m.Installer is null || m.Installer.Size<1 || m.Installer.Size>1024L*1024*1024 || m.Installer.PublisherThumbprint!=publisher) throw new IOException("Hub installer publisher or size rejected.");
        Metadata.ImmutableUrl(m.Installer.Url);Metadata.Hash(m.Installer.Sha256);return m;
    }
    public static async Task VerifyInstallerAsync(HubManifest m, string path, CancellationToken token)
    {
        if(!await InstallManager.Matches(path,m.Installer.Size,m.Installer.Sha256,token)) throw new IOException("Hub installer bytes failed verification.");
        if(!OperatingSystem.IsWindows()) throw new IOException("Windows publisher verification requires Windows.");
        WindowsPublisher.Verify(path,m.Installer.PublisherThumbprint);
    }
    public static async Task<string> StageAsync(string envelope, string root, DownloadClient downloads, IProgress<TransferProgress>? progress, CancellationToken token)
    {
        var m=Verify(envelope,PublicKey(),Publisher(),Highest(root),Environment.OSVersion.Version.Build);
        InstallManager.RejectLinks(root);Directory.CreateDirectory(root);
        InstallManager.RejectLinks(Path.Combine(root,"update.lock"));
        using var guard=new FileStream(Path.Combine(root,"update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var stage=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try {
            if(new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace<m.Installer.Size+64*1024*1024)throw new IOException("Not enough disk space for Hub update.");
            var path=Path.Combine(stage,"setup.exe");
            await downloads.DownloadAsync(new("hub",m.Installer.Url,m.Installer.Size,m.Installer.Sha256,Array.Empty<ReleaseFile>()),path,progress,token);
            await VerifyInstallerAsync(m,path,token);token.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(stage,"manifest.json"),envelope);return stage;
        }catch {Directory.Delete(stage,true);throw;}
    }
    public static async Task CancelHandoffAsync(Process ownedBootstrap,string stage)
    {
        // This is the Process returned by our own Start call, never a process rediscovered by PID.
        _ = ownedBootstrap.SafeHandle;
        try { File.WriteAllText(Path.Combine(stage,"cancel.requested"),"Canceled Hub handoff; never install."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A full/read-only stage must not leave a live installer handoff waiting for later Hub exit.
            try { if (!ownedBootstrap.HasExited) ownedBootstrap.Kill(); }
            finally { await ownedBootstrap.WaitForExitAsync(); }
        }
    }
    public static async Task WaitForParentExitAsync(Process parent,string stage,CancellationToken token)
    {
        // Retain the originating native process handle; PID reuse cannot replace this parent.
        _ = parent.SafeHandle;
        while (!parent.HasExited)
        {
            if (File.Exists(Path.Combine(stage,"cancel.requested"))) throw new OperationCanceledException("Hub handoff was canceled.");
            await Task.Delay(100,token);
        }
        token.ThrowIfCancellationRequested();
        if (File.Exists(Path.Combine(stage,"cancel.requested"))) throw new OperationCanceledException("Hub handoff was canceled.");
        await parent.WaitForExitAsync(token);
    }
    public static long Highest(string root)
    {
        var path=Path.Combine(root,"sequence.json");InstallManager.RejectLinks(path);
        return File.Exists(path)?JsonSerializer.Deserialize<long>(File.ReadAllText(path)):0;
    }
    public static void SaveSequence(string root,long sequence)
    {
        var path=Path.Combine(root,"sequence.json");InstallManager.RejectLinks(path);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,sequence);file.Flush(true);}File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
public static class WindowsPublisher
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct FileInfo {public uint Size;public IntPtr Path;public IntPtr Handle;public IntPtr Subject;}
    [StructLayout(LayoutKind.Sequential)] struct TrustData {public uint Size;public IntPtr Policy;public IntPtr Sip;public uint Ui;public uint Revocation;public uint Choice;public IntPtr File;public uint StateAction;public IntPtr State;public IntPtr Url;public uint Flags;public uint Context;public IntPtr Signature;}
    // Only the documented leading fields of CRYPT_PROVIDER_CERT are needed.
    [StructLayout(LayoutKind.Sequential)] struct ProviderCertificate {public uint Size;public IntPtr Context;}
    [DllImport("wintrust.dll",ExactSpelling=true,CharSet=CharSet.Unicode)] static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
    [DllImport("wintrust.dll",ExactSpelling=true)] static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll",ExactSpelling=true)] static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider,uint signer,[MarshalAs(UnmanagedType.Bool)] bool counterSigner,uint counterSignerIndex);
    [DllImport("wintrust.dll",ExactSpelling=true)] static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer,uint certificate);
    public static void Verify(string path,string fingerprint)
    {
        if(!Regex.IsMatch(fingerprint??"","^[A-F0-9]{64}$"))throw new CryptographicException("Reviewed publisher SHA256 pin is required.");
        if(!System.IO.File.Exists(path))throw new IOException("Installer file is missing.");
        if(!OperatingSystem.IsWindows())throw new IOException("Windows publisher verification requires Windows.");
        var pathPtr=Marshal.StringToCoTaskMemUni(path);var infoPtr=Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
        var data=new TrustData{Size=(uint)Marshal.SizeOf<TrustData>(),Ui=2,Revocation=1,Choice=1,File=infoPtr,StateAction=1,Flags=0x40};
        var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try {
            Marshal.StructureToPtr(new FileInfo{Size=(uint)Marshal.SizeOf<FileInfo>(),Path=pathPtr},infoPtr,false);
            if(WinVerifyTrust(new IntPtr(-1),ref action,ref data)!=0)throw new IOException("Windows rejected installer Authenticode trust or revocation status.");
            // Read the signer from this successful trust verification, not by
            // reparsing the file with the obsolete format-guessing loader.
            var provider=WTHelperProvDataFromStateData(data.State);
            var signer=provider==IntPtr.Zero?IntPtr.Zero:WTHelperGetProvSignerFromChain(provider,0,false,0);
            var entry=signer==IntPtr.Zero?IntPtr.Zero:WTHelperGetProvCertFromChain(signer,0);
            if(entry==IntPtr.Zero)throw new CryptographicException("Verified Authenticode signer unavailable.");
            var leaf=Marshal.PtrToStructure<ProviderCertificate>(entry);
            if(leaf.Size<Marshal.SizeOf<ProviderCertificate>() || leaf.Context==IntPtr.Zero)throw new CryptographicException("Verified signer certificate unavailable.");
            using var certificate=new X509Certificate2(leaf.Context);
            if(Convert.ToHexString(SHA256.HashData(certificate.RawData))!=fingerprint)throw new IOException("Installer was signed by an unrecognized publisher.");
        }finally{data.StateAction=2;WinVerifyTrust(new IntPtr(-1),ref action,ref data);Marshal.FreeHGlobal(infoPtr);Marshal.FreeCoTaskMem(pathPtr);}
    }
}
