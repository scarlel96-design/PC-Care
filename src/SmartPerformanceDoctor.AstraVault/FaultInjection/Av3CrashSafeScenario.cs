namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Mandatory crash-safe commit scenarios (E-0 §5).</summary>
public enum Av3CrashSafeScenario
{
    ObjectWriteBeforeCrash = 1,
    ObjectWriteDuringCrash = 2,
    MetadataWriteBeforeCrash = 3,
    MetadataWriteDuringCrash = 4,
    JournalWriteDuringCrash = 5,
    ObjectFlushFailure = 6,
    MetadataFlushFailure = 7,
    JournalFlushFailure = 8,
    ActivationHeaderWriteBeforeCrash = 9,
    ActivationHeaderWriteDuringCrash = 10,
    ActivationHeaderFlushFailure = 11,
    PostFlushRereadFailure = 12,
    PostFlushAeadAuthFailure = 13,
    HeaderCopyOneDurable = 14,
    HeaderCopyTwoDurable = 15,
    HeaderCopyThreeConflicting = 16,
    DiskFullSimulation = 17,
    ExternalDriveRemovalSimulation = 18,
    StaleHighGeneration = 19,
    EqualGenerationConflictingRoot = 20,
    CleanupDuringCrash = 21
}