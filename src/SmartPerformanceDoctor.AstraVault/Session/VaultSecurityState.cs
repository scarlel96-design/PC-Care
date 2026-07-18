namespace SmartPerformanceDoctor.AstraVault.Session;

/// <summary>UI·crypto 공통 보안 상태 (내역.txt §10).</summary>
public enum VaultSecurityState
{
    NotCreated,
    Locked,
    Unlocking,
    Unlocked,
    ReadOnlyUnlocked,
    StepUpRequired,
    Importing,
    Encrypting,
    Verifying,
    Committing,
    CleaningTempFiles,
    RedundancyDegraded,
    CorruptionDetected,
    RollbackSuspected,
    RecoveryAvailable,
    MigrationRequired,
    AutoLockScheduled
}