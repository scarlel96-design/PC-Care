namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Deterministic fault injection points (Phase E-1 test harness).</summary>
public enum Av3FaultPoint
{
    BeforeObjectWrite = 1,
    AfterObjectWriteBeforeFlush = 2,
    AfterObjectFlush = 3,
    BeforeMetadataWrite = 4,
    AfterMetadataWriteBeforeFlush = 5,
    AfterMetadataFlush = 6,
    BeforeJournalWrite = 7,
    AfterJournalWriteBeforeFlush = 8,
    AfterJournalFlush = 9,
    BeforeActivationHeaderWrite = 10,
    AfterActivationHeaderWriteBeforeFlush = 11,
    AfterActivationFlushBeforeReread = 12,
    AfterRereadBeforeAuthentication = 13,
    AfterAuthenticationBeforeCleanup = 14,
    DuringCleanup = 15
}