using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace EvidenceHub.Updates;
public record InstalledVersion(string Directory, string Envelope);
public record InstallState(InstalledVersion? Current = null, InstalledVersion? Previous = null, long HighestSequence = 0);
public sealed class InstallManager(string root, string publicKey, DownloadClient downloads, string saveFolder)
{
    public string Root => root;
    string StatePath => Path.Combine(root, "active.json");
    public InstallState State()
    {
        RejectLinks(root); RejectLinks(StatePath);
        var state = File.Exists(StatePath) ? JsonSerializer.Deserialize<InstallState>(File.ReadAllText(StatePath), Metadata.Json) ?? throw new IOException("Invalid installation state.") : new();
        if (state.HighestSequence < 0) throw new IOException("Invalid installation sequence.");
        foreach (var version in new[] { state.Current, state.Previous }.Where(v => v is not null)) DirectoryFor(version!);
        return state;
    }
    public static void RequireSpace(long required, long available)
    {
        if (required < 0 || available < required) throw new IOException("Not enough disk space to stage Genesis safely. Free storage or choose another new-install location.");
    }
    public VerifiedRelease VerifyNew(string envelope)
    {
        var state = State(); var release = Metadata.Verify(envelope, publicKey, state.HighestSequence);
        if (state.Current is not null)
        {
            var current = Metadata.Verify(state.Current.Envelope, publicKey, offlineInstalled: true).Manifest;
            if (Metadata.VersionOf(release.Manifest.Version) < Metadata.VersionOf(current.Version)) throw new IOException("Automatic downgrades are rejected; use explicit rollback.");
            if (release.Manifest.Sequence == state.HighestSequence && release.Envelope != state.Current.Envelope && (state.Previous is null || release.Envelope != state.Previous.Envelope)) throw new IOException("Conflicting metadata for an accepted release sequence.");
        }
        return release;
    }
    public void ClearDownloadStorage()
    {
        using var guard = Lock(); EnsureStopped(); var state = State();
        // Validate state before deleting any generated files; never touch versions or saves.
        foreach (var version in new[] { state.Current, state.Previous }.Where(v => v is not null))
        { DirectoryFor(version!); Metadata.Verify(version!.Envelope, publicKey, offlineInstalled: true); }
        foreach (var name in new[] { "cache", "staging" })
        {
            var folder = Path.Combine(root, name); RejectLinks(folder);
            if (!Directory.Exists(folder)) continue;
            // Enumerate and reject every linked child before doing any deletion.
            foreach (var entry in EnumerateFiles(folder)) RejectLinks(entry);
            Directory.Delete(folder, true);
        }
    }
    public long StorageBytes()
    {
        if (!Directory.Exists(root)) return 0;
        RejectLinks(root); return EnumerateFiles(root).Sum(path => new FileInfo(path).Length);
    }
    public string DirectoryFor(InstalledVersion version)
    {
        if (!Guid.TryParseExact(version.Directory, "N", out _)) throw new IOException("Invalid managed installation path.");
        return Path.Combine(root, "versions", version.Directory);
    }
    FileStream Lock()
    {
        Directory.CreateDirectory(root); RejectLinks(root);
        RejectLinks(StatePath); RejectLinks(Path.Combine(root, "install.lock"));
        return new(Path.Combine(root, "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public static void RejectLinks(string path)
    {
        var p = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(p))
        {
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked installation paths are not supported.");
            p = Path.GetDirectoryName(p)!;
        }
    }
    public static async Task<bool> Matches(string path, long length, string hash, CancellationToken token)
    {
        RejectLinks(path);
        if (!File.Exists(path) || new FileInfo(path).Length != length) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant() == hash;
    }
    public async Task<Installation?> CurrentAsync(CancellationToken token)
    {
        using var guard = Lock(); var state = State();
        if (state.Current is null) return null;
        var release = Metadata.Verify(state.Current.Envelope, publicKey, offlineInstalled: true);
        var folder = DirectoryFor(state.Current); await VerifyFiles(release.Manifest, folder, token);
        return Genesis.Detect(folder);
    }
    public async Task<System.Diagnostics.Process> LaunchAsync(string? importedFolder, CancellationToken token)
    {
        using var guard = Lock(); var state = State(); Installation game;
        if (state.Current is { } current)
        {
            var release = Metadata.Verify(current.Envelope, publicKey, offlineInstalled: true);
            await VerifyFiles(release.Manifest, DirectoryFor(current), token); game = Genesis.Detect(DirectoryFor(current));
        }
        else game = Genesis.Detect(importedFolder ?? "");
        token.ThrowIfCancellationRequested();
        return System.Diagnostics.Process.Start(Genesis.LaunchInfo(game)) ?? throw new IOException("Windows did not start Genesis.");
    }
    static async Task VerifyFiles(ReleaseManifest release, string folder, CancellationToken token)
    {
        foreach (var p in release.Packages)
            foreach (var f in p.Files)
                if (!await Matches(Path.Combine(folder, Metadata.Mount(p.Kind), f.Path), f.Size, f.Sha256, token)) throw new IOException("Installed file failed verification: " + f.Path);
    }
    static void EnsureStopped()
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("EvidenceGenesis"))
        { using (process) { if (!process.HasExited) throw new IOException("Close Genesis before installing or rolling back. The Hub will not stop your game."); } }
    }
    void WriteState(InstallState state)
    {
        var temp = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, state, Metadata.Json); file.Flush(true); }
            File.Move(temp, StatePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    async Task BackupSaves(CancellationToken token)
    {
        if (!Directory.Exists(saveFolder)) return;
        RejectLinks(saveFolder);
        var backup = Path.Combine(root, "save-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        foreach (var source in EnumerateFiles(saveFolder))
        {
            token.ThrowIfCancellationRequested();
            var dest = Path.Combine(backup, Path.GetRelativePath(saveFolder, source));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await Copy(source, dest, token);
        }
    }
    static IEnumerable<string> EnumerateFiles(string folder)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
        {
            RejectLinks(entry);
            if (Directory.Exists(entry)) { foreach (var child in EnumerateFiles(entry)) yield return child; }
            else yield return entry;
        }
    }
    static async Task Copy(string from, string to, CancellationToken token)
    {
        RejectLinks(from); RejectLinks(to);
        await using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        await using var output = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
        await input.CopyToAsync(output, token);
    }
    async Task Extract(ReleasePackage package, string archive, string target, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archive);
        var expected = package.Files.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested(); Metadata.SafePath(entry.FullName);
            if (!seen.Add(entry.FullName) || !expected.TryGetValue(entry.FullName, out var file) || entry.Length != file.Size ||
                ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & 0x400) != 0) throw new IOException("ZIP contains undeclared, linked or invalid files.");
            var dest = Path.Combine(target, entry.FullName); RejectLinks(dest); Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await using (var output = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            await using (var input = entry.Open())
            {
                var buffer = new byte[65536]; long count = 0; int n;
                while ((n = await input.ReadAsync(buffer, token)) > 0)
                { count += n; if (count > file.Size) throw new IOException("ZIP expansion limit exceeded."); await output.WriteAsync(buffer.AsMemory(0, n), token); }
                if (count != file.Size) throw new IOException("Truncated ZIP entry.");
            }
            if (!await Matches(dest, file.Size, file.Sha256, token)) throw new IOException("Extracted file hash failed verification.");
        }
        if (seen.Count != expected.Count) throw new IOException("ZIP is missing declared files.");
    }
    public async Task InstallAsync(string envelope, string? importedFolder, IProgress<TransferProgress>? progress, CancellationToken token, Action? validateAuthorization = null)
    {
        using var guard = Lock(); EnsureStopped(); var verified = VerifyNew(envelope); var m = verified.Manifest; var state = State();
        if (importedFolder is not null) importedFolder = Genesis.Detect(importedFolder).Folder;
        if (state.Current is not null)
        {
            var current = Metadata.Verify(state.Current.Envelope, publicKey, offlineInstalled: true).Manifest;
            if (current.SaveSchema != m.SaveSchema && !(current.SaveSchema == 1 && m.SaveSchema == 4 && current.Version == "0.3.0" && (m.Version == "0.4.0" || m.Version == "0.5.0") && Metadata.VersionOf(m.MinimumHubVersion) >= Metadata.VersionOf("0.3.0"))) throw new IOException("This save-schema transition is not reviewed. Only Genesis0.3.0 epoch1 to0.4.0/0.5.0 epoch4 is supported; legacy import is explicit and originals are preserved.");
            if (current.Sequence == m.Sequence && state.Current.Envelope == envelope) throw new IOException("This release is already installed.");
        }
        var stagingBase = Path.Combine(root, "staging"); Directory.CreateDirectory(stagingBase); RejectLinks(stagingBase);
        // Abandoned staging from a crashed install never affects the active pointer; remove only Hub-owned GUID directories.
        foreach (var abandoned in Directory.EnumerateDirectories(stagingBase))
        { RejectLinks(abandoned); if (Guid.TryParseExact(Path.GetFileName(abandoned), "N", out _)) { foreach (var file in EnumerateFiles(abandoned)) RejectLinks(file); Directory.Delete(abandoned, true); } }
        var id = Guid.NewGuid().ToString("N"); var stage = Path.Combine(stagingBase, id); Directory.CreateDirectory(stage);
        var final = Path.Combine(root, "versions", id); bool activated = false;
        try
        {
            var needed = m.Packages.Sum(p => p.Size + p.Files.Sum(f => f.Size));
            RequireSpace(needed + 64 * 1024 * 1024, new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace);
            foreach (var package in m.Packages)
            {
                token.ThrowIfCancellationRequested(); progress?.Report(new(package.Kind, 0, package.Size, "Verifying reusable files"));
                var target = Path.Combine(stage, Metadata.Mount(package.Kind)); Directory.CreateDirectory(target);
                var sources = new[] { state.Current is null ? null : DirectoryFor(state.Current), importedFolder }.Where(s => s is not null).Cast<string>();
                bool reused = false;
                foreach (var source in sources)
                {
                    var reusable = new Dictionary<ReleaseFile, string>();
                    foreach (var f in package.Files)
                    {
                        var candidates = new[] { Path.Combine(source, Metadata.Mount(package.Kind), f.Path),
                            f.Path.StartsWith("licenses/", StringComparison.Ordinal) ? Path.Combine(source, f.Path) : "" };
                        foreach (var candidate in candidates.Where(c => c.Length > 0))
                            if (await Matches(candidate, f.Size, f.Sha256, token)) { reusable[f] = candidate; break; }
                    }
                    if (reusable.Count != package.Files.Length) continue;
                    foreach (var f in package.Files)
                    {
                        var dest = Path.Combine(target, f.Path); Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        await Copy(reusable[f], dest, token);
                    }
                    reused = true; progress?.Report(new(package.Kind, 0, 0, "Reused verified files (no download)")); break;
                }
                if (reused) continue;
                var cache = Path.Combine(root, "cache", package.Sha256 + ".zip"); Directory.CreateDirectory(Path.GetDirectoryName(cache)!); RejectLinks(cache);
                if (!await Matches(cache, package.Size, package.Sha256, token))
                    await downloads.DownloadAsync(package, cache, progress, token);
                else progress?.Report(new(package.Kind, 0, 0, "Reused verified package cache"));
                progress?.Report(new(package.Kind, package.Size, package.Size, "Verifying and extracting"));
                await Extract(package, cache, target, token);
            }
            await VerifyFiles(m, stage, token); Genesis.Detect(stage); EnsureStopped();
            progress?.Report(new("saves", 0, 0, "Backing up existing saves")); await BackupSaves(token);
            token.ThrowIfCancellationRequested(); Metadata.Verify(envelope, publicKey, state.HighestSequence); validateAuthorization?.Invoke(); Directory.CreateDirectory(Path.GetDirectoryName(final)!); RejectLinks(final);
            Directory.Move(stage, final);
            // No awaits between final cancellation check and atomic activation.
            token.ThrowIfCancellationRequested(); WriteState(new(new(id, envelope), state.Current, Math.Max(state.HighestSequence, m.Sequence))); activated = true;
            progress?.Report(new("genesis", 0, 0, "Installed and verified"));
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            if (!activated && Directory.Exists(final)) Directory.Delete(final, true);
        }
    }
    public async Task RollbackAsync(CancellationToken token)
    {
        using var guard = Lock(); EnsureStopped(); var state = State();
        if (state.Current is null || state.Previous is null) throw new IOException("No previous installation is available.");
        var current = Metadata.Verify(state.Current.Envelope, publicKey, offlineInstalled: true).Manifest;
        var previous = Metadata.Verify(state.Previous.Envelope, publicKey, offlineInstalled: true).Manifest;
        if (current.SaveSchema != previous.SaveSchema) throw new IOException("Rollback cannot cross save schemas.");
        await VerifyFiles(previous, DirectoryFor(state.Previous), token); Genesis.Detect(DirectoryFor(state.Previous));
        await BackupSaves(token); EnsureStopped(); token.ThrowIfCancellationRequested();
        WriteState(state with { Current = state.Previous, Previous = state.Current });
    }
}
