using System.Diagnostics;
using System.Text.Json;

namespace EvidenceHub;
public record HubSettings(string? GenesisFolder = null, bool MinimizeOnPlay = false);
public record Installation(string Folder, string Executable, bool HasRuntime, bool HasModel);
public static class Genesis
{
    public static Installation Detect(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            throw new IOException("Choose an existing Genesis installation folder.");
        folder = Path.GetFullPath(folder);
        // Only documented package layouts; never recursively scan a disk or select arbitrary executables.
        var candidates = new[] { folder, Path.Combine(folder, "EvidenceGenesis") }
            .Select(p => Path.Combine(p, "EvidenceGenesis.exe")).Where(File.Exists).ToArray();
        if (candidates.Length == 0) throw new IOException("EvidenceGenesis.exe was not found. Choose the extracted game folder, not the ZIP or model folder.");
        if (candidates.Length > 1) throw new IOException("Two Genesis executables were found. Choose the specific game folder.");
        var exe = candidates[0];
        using (var stream = File.OpenRead(exe))
        using (var reader = new BinaryReader(stream))
        {
            if (stream.Length < 64 || reader.ReadUInt16() != 0x5A4D) throw new IOException("Genesis executable is not a valid Windows program.");
            stream.Position = 0x3c;
            var offset = reader.ReadInt32();
            if (offset < 64 || offset > stream.Length - 6) throw new IOException("Genesis executable has an invalid PE header.");
            stream.Position = offset;
            if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x8664)
                throw new IOException("Select a Windows x64 Genesis build.");
        }
        var actual = Path.GetDirectoryName(exe)!;
        return new(actual, exe, File.Exists(Path.Combine(actual, "brain", "llama-server.exe")),
            File.Exists(Path.Combine(actual, "model", "genesis.gguf")));
    }
    public static ProcessStartInfo LaunchInfo(Installation installation) => new(installation.Executable)
    {
        WorkingDirectory = installation.Folder, UseShellExecute = false
    };
}
public sealed class SettingsStore(string path)
{
    public HubSettings Load()
    {
        if (!File.Exists(path)) return new();
        return JsonSerializer.Deserialize<HubSettings>(File.ReadAllText(path))
            ?? throw new IOException("Settings are empty. The original file has been preserved.");
    }
    public void Save(HubSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
