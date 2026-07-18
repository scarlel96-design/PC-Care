namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 disk durability failure taxonomy (public codes only).</summary>
public enum Av3DiskDurabilityFailureReason
{
    None = 0,
    UnsupportedFilesystem = 1,
    RemovableMediaWithoutPolicy = 2,
    NetworkPathNoProductionWriter = 3,
    CloudSyncPathNoProductionWriter = 4,
    UnknownFilesystemFailClosed = 5,
    OutOfSpace = 6,
    AccessDenied = 7,
    FileLockExhausted = 8,
    SurpriseRemovalRecoveryRequired = 9,
    DirectorySyncUnsupported = 10,
    FlushRereadFailed = 11,
    RenameReplaceFailed = 12,
    StaleTempRecoveryRequired = 13,
    PowerLossBeforeHeaderNoPromotion = 14,
    PowerLossBeforeRevalidationRecoveryRequired = 15,
    CleanupFailureNoTrustedPromotion = 16,
    HarnessOnlyRequired = 17,
    IsolatedRootRequired = 18
}