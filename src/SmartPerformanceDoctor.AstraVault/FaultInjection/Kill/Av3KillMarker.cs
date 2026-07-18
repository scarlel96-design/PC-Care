namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

/// <summary>Child-process kill injection point (maps to <see cref="Av3FaultPoint"/>).</summary>
public static class Av3KillMarker
{
    public static Av3FaultPoint[] All { get; } =
    [
        Av3FaultPoint.BeforeObjectWrite,
        Av3FaultPoint.AfterObjectWriteBeforeFlush,
        Av3FaultPoint.AfterObjectFlush,
        Av3FaultPoint.BeforeMetadataWrite,
        Av3FaultPoint.AfterMetadataWriteBeforeFlush,
        Av3FaultPoint.AfterMetadataFlush,
        Av3FaultPoint.BeforeJournalWrite,
        Av3FaultPoint.AfterJournalWriteBeforeFlush,
        Av3FaultPoint.AfterJournalFlush,
        Av3FaultPoint.BeforeActivationHeaderWrite,
        Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush,
        Av3FaultPoint.AfterActivationFlushBeforeReread,
        Av3FaultPoint.AfterRereadBeforeAuthentication,
        Av3FaultPoint.AfterAuthenticationBeforeCleanup,
        Av3FaultPoint.DuringCleanup
    ];
}