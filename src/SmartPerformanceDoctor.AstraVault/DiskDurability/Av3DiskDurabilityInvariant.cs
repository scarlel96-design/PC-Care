namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 disk durability invariant identifiers.</summary>
public enum Av3DiskDurabilityInvariant
{
    DiskDurabilityRequiredForWriterEnable = 1,
    UnsupportedFilesystemNoProductionWriter = 2,
    RemovableMediaNoProductionWriterWithoutPolicy = 3,
    NetworkPathNoProductionWriter = 4,
    CloudSyncPathNoProductionWriter = 5,
    FlushRereadRequiredBeforeTrustedPromotion = 6,
    DirectorySyncUnsupportedRequiresReview = 7,
    OutOfSpaceNotCommitted = 8,
    AccessDeniedNotCommitted = 9,
    FileLockNotCommittedWithoutSafeRetry = 10,
    StaleTempNoTrustedPromotion = 11,
    DiskDurabilityPublicErrorSafe = 12,
    DiskDurabilityNoSecretLeak = 13,
    ActualDiskDurabilityNotMarkedReviewedWithoutSignoff = 14
}