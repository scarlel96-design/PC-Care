namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>User-media / filesystem posture for production writer discussion (not enable).</summary>
public enum Av3DiskDurabilityClassification
{
    UnknownFailClosed = 0,
    NtfsFixedDiskCandidate = 1,
    ReFsReviewRequired = 2,
    ExFatRestricted = 3,
    RemovableRestricted = 4,
    NetworkPathNoProductionWriter = 5,
    CloudSyncNoProductionWriter = 6,
    HarnessSyntheticOnly = 7,
    RecoveryRequired = 8
}