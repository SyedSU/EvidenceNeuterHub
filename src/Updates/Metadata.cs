using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EvidenceHub.Updates;
public record ReleaseFile(string Path, long Size, string Sha256);
public record ReleasePackage(string Kind, string Url, long Size, string Sha256, ReleaseFile[] Files);
public record ReleaseManifest(int Schema, string Product, string Channel, string Platform, string Version,
    long Sequence, DateTimeOffset Expires, string MinimumHubVersion, int SaveSchema, string SourceCommit, ReleasePackage[] Packages);
public record SignedRelease(string Payload, string Signature);
public record VerifiedRelease(ReleaseManifest Manifest, string Envelope);
public static class Metadata
{
    public const string HubVersion = "0.2.0";
    public const string CatalogUrl = "https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/genesis-stable/genesis-stable.json";
    public const int MaxMetadataBytes = 2 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static Version VersionOf(string value)
    {
        if (!Regex.IsMatch(value ?? "", @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")) throw new IOException("Invalid release version.");
        return Version.Parse(value!);
    }
    public static VerifiedRelease Verify(string envelope, string publicKey, long minimumSequence = 0, bool offlineInstalled = false)
    {
        if (string.IsNullOrWhiteSpace(publicKey)) throw new IOException("Release trust is not configured in this Hub build. Existing installations remain playable; downloads are disabled until a trusted release build is supplied.");
        if (System.Text.Encoding.UTF8.GetByteCount(envelope) > MaxMetadataBytes) throw new IOException("Release metadata is too large.");
        var signed = JsonSerializer.Deserialize<SignedRelease>(envelope, Json) ?? throw new IOException("Empty release metadata.");
        var payload = Convert.FromBase64String(signed.Payload);
        using var rsa = RSA.Create(); rsa.ImportFromPem(publicKey);
        if (rsa.KeySize < 3072 || !rsa.VerifyData(payload, Convert.FromBase64String(signed.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new IOException("Release signature is invalid. Nothing was installed.");
        var m = JsonSerializer.Deserialize<ReleaseManifest>(payload, Json) ?? throw new IOException("Empty release payload.");
        if (m.Schema != 1 || m.Product != "genesis" || m.Channel != "stable" || m.Platform != "windows-x64" || m.Sequence < 1 || m.SaveSchema < 1)
            throw new IOException("Unsupported release metadata.");
        if (!Regex.IsMatch(m.SourceCommit ?? "", "^[a-f0-9]{40}$")) throw new IOException("Missing source provenance.");
        VersionOf(m.Version);
        if (VersionOf(m.MinimumHubVersion) > VersionOf(HubVersion)) throw new IOException("This Genesis release requires a newer Hub.");
        if (!offlineInstalled && (m.Expires <= DateTimeOffset.UtcNow || m.Sequence < minimumSequence)) throw new IOException("Release metadata expired or is older than an accepted release. Existing games remain playable offline.");
        if (m.Packages is null || m.Packages.Length != 3 || !m.Packages.Select(p => p.Kind).Order().SequenceEqual(new[] { "game", "model", "runtime" }))
            throw new IOException("Release must contain separate game, runtime and model packages.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var p in m.Packages)
        {
            PackageUrl(p.Url); Hash(p.Sha256);
            if (p.Size < 1 || p.Size > 4L * 1024 * 1024 * 1024 || p.Files is null || p.Files.Length < 1 || p.Files.Length > 4096) throw new IOException("Package limits exceeded.");
            foreach (var f in p.Files)
            {
                SafePath(f.Path); Hash(f.Sha256);
                if (f.Size < 0 || f.Size > 4L * 1024 * 1024 * 1024) throw new IOException("Invalid file length.");
                total = checked(total + f.Size);
                if (!paths.Add(Mount(p.Kind) + f.Path)) throw new IOException("Duplicate package path.");
                // Components may not shadow each other or activation metadata.
                if (p.Kind == "game" && (f.Path.StartsWith("brain/", StringComparison.OrdinalIgnoreCase) || f.Path.StartsWith("model/", StringComparison.OrdinalIgnoreCase) || f.Path.Equals("release.json", StringComparison.OrdinalIgnoreCase))) throw new IOException("Game package overlaps a reserved component path.");
            }
        }
        if (total > 8L * 1024 * 1024 * 1024 || !m.Packages.Single(p => p.Kind == "game").Files.Any(f => f.Path == "EvidenceGenesis.exe") ||
            !m.Packages.Single(p => p.Kind == "runtime").Files.Any(f => f.Path == "llama-server.exe") ||
            !m.Packages.Single(p => p.Kind == "model").Files.Any(f => f.Path == "genesis.gguf")) throw new IOException("Incomplete or oversized Genesis release.");
        return new(m, envelope);
    }
    public static string Mount(string kind) => kind switch { "game" => "", "runtime" => "brain/", "model" => "model/", _ => throw new IOException("Unknown component.") };
    public static void Hash(string value) { if (!Regex.IsMatch(value ?? "", "^[a-f0-9]{64}$")) throw new IOException("Invalid SHA-256."); }
    public static void SafePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 180 || path.Contains('\\') || path.StartsWith('/') || path.Split('/').Any(s =>
            s.Length == 0 || s is "." or ".." || s.EndsWith('.') || s.EndsWith(' ') || s.Any(c => c < 32 || "<>:\"|?*".Contains(c)) ||
            Regex.IsMatch(s, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase))) throw new IOException("Unsafe package path.");
    }
    public static Uri PackageUrl(string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.AbsolutePath.StartsWith("/SyedSU/EvidenceNeuter-Releases/releases/download/", StringComparison.Ordinal)) throw new IOException("Package URL is outside the public releases repository.");
        return uri;
    }
}
