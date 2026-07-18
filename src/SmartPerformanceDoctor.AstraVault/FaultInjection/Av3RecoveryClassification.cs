namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Crash recovery outcome (fail-closed when uncertain).</summary>
public enum Av3RecoveryClassification
{
    PreviousGenerationOpen = 1,
    NewGenerationOpen = 2,
    RecoveryRequired = 3,
    RedundancyDegraded = 4,
    CorruptBlocked = 5,
    RollbackSuspected = 6,
    Aborted = 7,
    UnknownFailClosed = 8
}