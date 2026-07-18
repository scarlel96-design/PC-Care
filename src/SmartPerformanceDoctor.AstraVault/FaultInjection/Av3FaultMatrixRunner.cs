using SmartPerformanceDoctor.AstraVault.Experimental;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Runs fault-point + crash-safe FI matrix (harness-only).</summary>
public static class Av3FaultMatrixRunner
{
    public static Av3FaultMatrixReport RunAll(Func<Av3WritePlan> planFactory)
    {
        var entries = new List<Av3FaultMatrixReportEntry>();

        foreach (var row in Av3FaultMatrix.AllRows)
        {
            entries.Add(RunFaultPointRow(row, planFactory));
        }

        foreach (var row in Av3CrashSafeScenarioMatrix.AllRows)
        {
            entries.Add(RunCrashSafeRow(row, planFactory));
        }

        var passed = entries.Count(e => e.Pass);
        return new Av3FaultMatrixReport
        {
            Total = entries.Count,
            Passed = passed,
            Failed = entries.Count - passed,
            Entries = entries
        };
    }

    private static Av3FaultMatrixReportEntry RunFaultPointRow(
        Av3FaultMatrix.MatrixRow row,
        Func<Av3WritePlan> planFactory)
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(planFactory(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = row.Point,
                FailFlush = row.RequiresFlushFailure,
                FailReread = row.RequiresFailReread,
                FailAuthentication = row.RequiresFailAuthentication
            }));

        return new Av3FaultMatrixReportEntry
        {
            ScenarioId = $"fault_point_{(int)row.Point}",
            Kind = "fault_point",
            Expected = row.Expected,
            Actual = result.Classification,
            Pass = row.Expected == result.Classification,
            DurabilityMode = row.RequiresFlushFailure
                ? Av3DurabilitySimulationMode.SimulatedFlushFailure
                : row.RequiresFailReread
                    ? Av3DurabilitySimulationMode.SimulatedPostFlushRereadFailure
                    : Av3DurabilitySimulationMode.SimulatedProcessKill
        };
    }

    private static Av3FaultMatrixReportEntry RunCrashSafeRow(
        Av3CrashSafeScenarioMatrix.ScenarioRow row,
        Func<Av3WritePlan> planFactory)
    {
        if (row.ClassifierOnly)
        {
            var actual = ClassifyScenarioOnly(row.Scenario);
            return new Av3FaultMatrixReportEntry
            {
                ScenarioId = $"crash_safe_{(int)row.Scenario}",
                Kind = "crash_safe",
                Expected = row.Expected,
                Actual = actual,
                Pass = row.Expected == actual,
                DurabilityMode = row.DurabilityMode
            };
        }

        using var storage = row.Scenario == Av3CrashSafeScenario.CleanupDuringCrash
            ? Av3TestStorage.CreateIsolatedCleanupRoot()
            : new Av3TestStorage();

        if (row.DurabilityMode == Av3DurabilitySimulationMode.SimulatedDiskFull)
        {
            storage.SimulateDiskFullOnNextWrite = true;
        }

        if (row.DurabilityMode == Av3DurabilitySimulationMode.SimulatedExternalMediaRemoved)
        {
            storage.SimulateIoFailureOnNextWrite = true;
        }

        Av3CommitResult result;
        try
        {
            result = Av3ExperimentalWriter.SimulateCommit(
                Av3WriteTransaction.CreateForTestHarness(planFactory(), storage),
                new Av3FaultInjector(new Av3FaultInjectionScenario
                {
                    FaultPoint = row.FaultPoint!.Value,
                    FailFlush = row.FailFlush,
                    FailReread = row.FailReread,
                    FailAuthentication = row.FailAuthentication
                }));
        }
        catch (Av3SimulatedIoException)
        {
            result = new Av3CommitResult
            {
                Completed = false,
                Classification = Av3RecoveryClassification.Aborted
            };
        }

        return new Av3FaultMatrixReportEntry
        {
            ScenarioId = $"crash_safe_{(int)row.Scenario}",
            Kind = "crash_safe",
            Expected = row.Expected,
            Actual = result.Classification,
            Pass = row.Expected == result.Classification,
            DurabilityMode = row.DurabilityMode
        };
    }

    private static Av3RecoveryClassification ClassifyScenarioOnly(Av3CrashSafeScenario scenario)
    {
        var snapshot = scenario switch
        {
            Av3CrashSafeScenario.HeaderCopyOneDurable => new Av3CommitSnapshot
            {
                ActivationAuthenticated = true,
                MetadataAuthenticated = true,
                HeaderCopyDurableCount = 1,
                CleanupCompleted = true
            },
            Av3CrashSafeScenario.HeaderCopyTwoDurable => new Av3CommitSnapshot
            {
                ActivationAuthenticated = true,
                MetadataAuthenticated = true,
                HeaderCopyDurableCount = 2,
                CleanupCompleted = true
            },
            Av3CrashSafeScenario.HeaderCopyThreeConflicting => new Av3CommitSnapshot
            {
                HeaderCopyConflict = true
            },
            Av3CrashSafeScenario.DiskFullSimulation => new Av3CommitSnapshot { Aborted = true, DiskFull = true },
            Av3CrashSafeScenario.ExternalDriveRemovalSimulation => new Av3CommitSnapshot { Aborted = true, ExternalMediaRemoved = true },
            Av3CrashSafeScenario.StaleHighGeneration => new Av3CommitSnapshot { StaleHighGenerationUnauthenticated = true },
            Av3CrashSafeScenario.EqualGenerationConflictingRoot => new Av3CommitSnapshot { EqualGenerationConflictingRoot = true },
            _ => new Av3CommitSnapshot()
        };

        return Av3RecoveryClassifier.Classify(snapshot);
    }
}