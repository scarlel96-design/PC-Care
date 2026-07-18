namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Expected recovery class per mandatory FI point (Phase E-2 automated matrix).</summary>
public static class Av3FaultMatrix
{
    public sealed record MatrixRow(
        Av3FaultPoint Point,
        Av3RecoveryClassification Expected,
        bool RequiresFlushFailure = false,
        bool RequiresFailReread = false,
        bool RequiresFailAuthentication = false);

    public static IReadOnlyList<MatrixRow> AllRows { get; } =
    [
        new(Av3FaultPoint.BeforeObjectWrite, Av3RecoveryClassification.PreviousGenerationOpen),
        new(Av3FaultPoint.AfterObjectWriteBeforeFlush, Av3RecoveryClassification.PreviousGenerationOpen),
        new(Av3FaultPoint.AfterObjectWriteBeforeFlush, Av3RecoveryClassification.Aborted, RequiresFlushFailure: true),
        new(Av3FaultPoint.AfterObjectFlush, Av3RecoveryClassification.RecoveryRequired),
        new(Av3FaultPoint.BeforeMetadataWrite, Av3RecoveryClassification.PreviousGenerationOpen),
        new(Av3FaultPoint.AfterMetadataWriteBeforeFlush, Av3RecoveryClassification.PreviousGenerationOpen),
        new(Av3FaultPoint.AfterMetadataWriteBeforeFlush, Av3RecoveryClassification.Aborted, RequiresFlushFailure: true),
        new(Av3FaultPoint.AfterMetadataFlush, Av3RecoveryClassification.RecoveryRequired),
        new(Av3FaultPoint.BeforeJournalWrite, Av3RecoveryClassification.PreviousGenerationOpen),
        new(Av3FaultPoint.AfterJournalWriteBeforeFlush, Av3RecoveryClassification.PreviousGenerationOpen),
        new(Av3FaultPoint.AfterJournalWriteBeforeFlush, Av3RecoveryClassification.Aborted, RequiresFlushFailure: true),
        new(Av3FaultPoint.AfterJournalFlush, Av3RecoveryClassification.RecoveryRequired),
        new(Av3FaultPoint.BeforeActivationHeaderWrite, Av3RecoveryClassification.PreviousGenerationOpen),
        new(Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush, Av3RecoveryClassification.RecoveryRequired),
        new(Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush, Av3RecoveryClassification.RecoveryRequired, RequiresFlushFailure: true),
        new(Av3FaultPoint.AfterActivationFlushBeforeReread, Av3RecoveryClassification.RecoveryRequired),
        new(Av3FaultPoint.AfterActivationFlushBeforeReread, Av3RecoveryClassification.CorruptBlocked, RequiresFailReread: true),
        new(Av3FaultPoint.AfterRereadBeforeAuthentication, Av3RecoveryClassification.CorruptBlocked),
        new(Av3FaultPoint.AfterRereadBeforeAuthentication, Av3RecoveryClassification.CorruptBlocked, RequiresFailAuthentication: true),
        new(Av3FaultPoint.AfterAuthenticationBeforeCleanup, Av3RecoveryClassification.RecoveryRequired),
        new(Av3FaultPoint.DuringCleanup, Av3RecoveryClassification.RecoveryRequired)
    ];

    public static IEnumerable<object[]> TheoryData() =>
        AllRows.Select(row => new object[] { row });
}