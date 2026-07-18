namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>Maps probe evidence to durability classification.</summary>
public static class Av3DiskDurabilityClassifier
{
    public static Av3DiskDurabilityCapability ClassifyPath(
        string normalizedPath,
        string? filesystemLabel,
        bool isRemovable,
        bool isNetwork,
        bool isCloudSync,
        bool isHarnessE14)
    {
        if (isHarnessE14)
        {
            return Capability(
                "harness-synthetic",
                classification: Av3DiskDurabilityClassification.HarnessSyntheticOnly,
                productionAllowed: false,
                fixedDisk: true,
                dirFsync: false);
        }

        if (isCloudSync)
        {
            return Capability(
                "cloud-sync",
                classification: Av3DiskDurabilityClassification.CloudSyncNoProductionWriter,
                productionAllowed: false);
        }

        if (isNetwork)
        {
            return Capability(
                "network-share",
                classification: Av3DiskDurabilityClassification.NetworkPathNoProductionWriter,
                productionAllowed: false);
        }

        if (isRemovable)
        {
            return Capability(
                filesystemLabel ?? "removable",
                classification: Av3DiskDurabilityClassification.RemovableRestricted,
                productionAllowed: false,
                removable: true);
        }

        var fs = (filesystemLabel ?? "unknown").ToUpperInvariant();
        return fs switch
        {
            "NTFS" => Capability(
                "NTFS",
                classification: Av3DiskDurabilityClassification.NtfsFixedDiskCandidate,
                productionAllowed: false,
                fixedDisk: true,
                atomicRename: true,
                dirFsync: false),
            "REFS" => Capability(
                "REFS",
                classification: Av3DiskDurabilityClassification.ReFsReviewRequired,
                productionAllowed: false,
                fixedDisk: true),
            "EXFAT" or "FAT32" => Capability(
                fs,
                classification: Av3DiskDurabilityClassification.ExFatRestricted,
                productionAllowed: false),
            "UNKNOWN" or "" => Capability(
                "unknown",
                classification: Av3DiskDurabilityClassification.UnknownFailClosed,
                productionAllowed: false),
            _ => Capability(
                fs,
                classification: Av3DiskDurabilityClassification.UnknownFailClosed,
                productionAllowed: false)
        };
    }

    private static Av3DiskDurabilityCapability Capability(
        string label,
        Av3DiskDurabilityClassification classification,
        bool productionAllowed,
        bool fixedDisk = false,
        bool removable = false,
        bool atomicRename = false,
        bool dirFsync = false) =>
        new()
        {
            FilesystemLabel = label,
            Classification = classification,
            ProductionWriterAllowed = productionAllowed,
            IsLocalFixedDisk = fixedDisk,
            IsRemovable = removable,
            AtomicRenameLikely = atomicRename,
            DirectoryFsyncOrEquivalent = dirFsync,
            WriteThroughFlushPolicy = fixedDisk
        };
}