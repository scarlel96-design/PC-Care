namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>E-0 §5 crash-safe scenarios mapped to harness injection + expected class.</summary>
public static class Av3CrashSafeScenarioMatrix
{
    public sealed record ScenarioRow(
        Av3CrashSafeScenario Scenario,
        Av3RecoveryClassification Expected,
        Av3FaultPoint? FaultPoint = null,
        bool FailFlush = false,
        bool FailReread = false,
        bool FailAuthentication = false,
        Av3DurabilitySimulationMode? DurabilityMode = null,
        bool ClassifierOnly = false);

    public static IReadOnlyList<ScenarioRow> AllRows { get; } =
    [
        new(Av3CrashSafeScenario.ObjectWriteBeforeCrash, Av3RecoveryClassification.PreviousGenerationOpen, Av3FaultPoint.BeforeObjectWrite),
        new(Av3CrashSafeScenario.ObjectWriteDuringCrash, Av3RecoveryClassification.PreviousGenerationOpen, Av3FaultPoint.AfterObjectWriteBeforeFlush),
        new(Av3CrashSafeScenario.MetadataWriteBeforeCrash, Av3RecoveryClassification.PreviousGenerationOpen, Av3FaultPoint.BeforeMetadataWrite),
        new(Av3CrashSafeScenario.MetadataWriteDuringCrash, Av3RecoveryClassification.PreviousGenerationOpen, Av3FaultPoint.AfterMetadataWriteBeforeFlush),
        new(Av3CrashSafeScenario.JournalWriteDuringCrash, Av3RecoveryClassification.PreviousGenerationOpen, Av3FaultPoint.AfterJournalWriteBeforeFlush),
        new(Av3CrashSafeScenario.ObjectFlushFailure, Av3RecoveryClassification.Aborted, Av3FaultPoint.AfterObjectWriteBeforeFlush, FailFlush: true, DurabilityMode: Av3DurabilitySimulationMode.SimulatedFlushFailure),
        new(Av3CrashSafeScenario.MetadataFlushFailure, Av3RecoveryClassification.Aborted, Av3FaultPoint.AfterMetadataWriteBeforeFlush, FailFlush: true, DurabilityMode: Av3DurabilitySimulationMode.SimulatedFlushFailure),
        new(Av3CrashSafeScenario.JournalFlushFailure, Av3RecoveryClassification.Aborted, Av3FaultPoint.AfterJournalWriteBeforeFlush, FailFlush: true, DurabilityMode: Av3DurabilitySimulationMode.SimulatedFlushFailure),
        new(Av3CrashSafeScenario.ActivationHeaderWriteBeforeCrash, Av3RecoveryClassification.PreviousGenerationOpen, Av3FaultPoint.BeforeActivationHeaderWrite),
        new(Av3CrashSafeScenario.ActivationHeaderWriteDuringCrash, Av3RecoveryClassification.RecoveryRequired, Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush),
        new(Av3CrashSafeScenario.ActivationHeaderFlushFailure, Av3RecoveryClassification.RecoveryRequired, Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush, FailFlush: true, DurabilityMode: Av3DurabilitySimulationMode.SimulatedFlushFailure),
        new(Av3CrashSafeScenario.PostFlushRereadFailure, Av3RecoveryClassification.CorruptBlocked, Av3FaultPoint.AfterActivationFlushBeforeReread, FailReread: true, DurabilityMode: Av3DurabilitySimulationMode.SimulatedPostFlushRereadFailure),
        new(Av3CrashSafeScenario.PostFlushAeadAuthFailure, Av3RecoveryClassification.CorruptBlocked, Av3FaultPoint.AfterRereadBeforeAuthentication, FailAuthentication: true),
        new(Av3CrashSafeScenario.HeaderCopyOneDurable, Av3RecoveryClassification.RedundancyDegraded, ClassifierOnly: true),
        new(Av3CrashSafeScenario.HeaderCopyTwoDurable, Av3RecoveryClassification.NewGenerationOpen, ClassifierOnly: true),
        new(Av3CrashSafeScenario.HeaderCopyThreeConflicting, Av3RecoveryClassification.CorruptBlocked, ClassifierOnly: true),
        new(Av3CrashSafeScenario.DiskFullSimulation, Av3RecoveryClassification.Aborted, DurabilityMode: Av3DurabilitySimulationMode.SimulatedDiskFull, ClassifierOnly: true),
        new(Av3CrashSafeScenario.ExternalDriveRemovalSimulation, Av3RecoveryClassification.Aborted, DurabilityMode: Av3DurabilitySimulationMode.SimulatedExternalMediaRemoved, ClassifierOnly: true),
        new(Av3CrashSafeScenario.StaleHighGeneration, Av3RecoveryClassification.PreviousGenerationOpen, ClassifierOnly: true),
        new(Av3CrashSafeScenario.EqualGenerationConflictingRoot, Av3RecoveryClassification.CorruptBlocked, ClassifierOnly: true),
        new(Av3CrashSafeScenario.CleanupDuringCrash, Av3RecoveryClassification.RecoveryRequired, Av3FaultPoint.DuringCleanup, DurabilityMode: Av3DurabilitySimulationMode.SimulatedProcessKill)
    ];

    public static IEnumerable<object[]> TheoryData() =>
        AllRows.Select(row => new object[] { row });
}