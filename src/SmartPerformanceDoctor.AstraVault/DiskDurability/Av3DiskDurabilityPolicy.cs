using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 durable write policy constants (production writer still disabled).</summary>
public static class Av3DiskDurabilityPolicy
{
    public const ulong MinimumFreeBytesThreshold = 4 * 1024 * 1024;

    public static bool HarnessDurabilityClosedIsNotProductionDiskClosed => true;

    public static bool FlushSuccessDoesNotGuaranteePhysicalMedia =>
        Av3PhaseGate.E14DiskDurabilityReviewPackageComplete;

    public static bool RemovableNetworkCloudSyncRequireExplicitPolicy => true;

    public static bool UnknownFilesystemFailClosed => true;

    public static bool ActualDiskDurabilityReviewed =>
        Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed;

    public static bool ActualDiskDurabilityReviewCandidate =>
        Av3PhaseGate.ActualDiskDurabilityReviewCandidate;

    public static bool ProductionDiskDurabilityClosed => false;

    public static int FileLockRetryMaxAttempts => 3;
}