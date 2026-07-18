namespace SmartPerformanceDoctor.AstraVault.Durable;

/// <summary>Relative paths under isolated harness vault root (test-only).</summary>
public static class Av3DurableFileLayout
{
    public const string ManifestRelative = "harness/durable-manifest.json";
    public const string KillMarkerReachedRelative = "harness/kill-marker-reached.txt";
    public const string ProgressRelative = "harness/progress-step.txt";
    public const string ObjectsRelative = "objects/set.bin";
    public const string MetadataRelative = "metadata/root.enc";
    public const string JournalRelative = "journal/current.jnal";
    public const string ActivationRelative = "header/activation.bin";
    public const string HeaderCopy0 = "header/copy-0.bin";
    public const string HeaderCopy1 = "header/copy-1.bin";
    public const string HeaderCopy2 = "header/copy-2.bin";

    public static string HeaderCopyRelative(byte copyIndex) => copyIndex switch
    {
        0 => HeaderCopy0,
        1 => HeaderCopy1,
        2 => HeaderCopy2,
        _ => throw new ArgumentOutOfRangeException(nameof(copyIndex))
    };
}