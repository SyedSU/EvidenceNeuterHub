namespace EvidenceHub.Updates;
public static class ReleaseTrust
{
    public static string PublicKey()
    {
        using var stream = typeof(ReleaseTrust).Assembly.GetManifestResourceStream("EvidenceHub.GenesisReleaseKey");
        return stream is null ? "" : new StreamReader(stream).ReadToEnd();
    }
}
