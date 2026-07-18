namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>Observed durable-write capabilities for a path (probe output).</summary>
public sealed class Av3DiskDurabilityCapability
{
    public string FilesystemLabel { get; init; } = "unknown";

    public bool IsLocalFixedDisk { get; init; }

    public bool IsRemovable { get; init; }

    public bool IsNetworkShare { get; init; }

    public bool IsCloudSyncedPath { get; init; }

    public bool AtomicRenameLikely { get; init; }

    public bool DirectoryFsyncOrEquivalent { get; init; }

    public bool WriteThroughFlushPolicy { get; init; }

    public bool LongPathSupported { get; init; }

    public Av3DiskDurabilityClassification Classification { get; init; }

    public bool ProductionWriterAllowed { get; init; }
}