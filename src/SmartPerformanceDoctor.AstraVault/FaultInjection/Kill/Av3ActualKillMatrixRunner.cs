using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.Repair;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

public static class Av3ActualKillMatrixRunner
{
    public static Av3KillReport RunCompareAll(Func<Av3WritePlan> planFactory)
    {
        var status = Av3ChildProcessKillHarness.SupportStatus;
        if (status != Av3KillSupportStatus.Supported)
        {
            return new Av3KillReport
            {
                SupportStatus = status,
                Total = 0,
                Passed = 0,
                Mismatched = 0
            };
        }

        var entries = new List<Av3KillReportEntry>();
        foreach (var marker in Av3KillMarker.All)
        {
            entries.Add(CompareMarker(marker, planFactory));
        }

        var passed = entries.Count(e => e.Match);
        return new Av3KillReport
        {
            SupportStatus = status,
            Total = entries.Count,
            Passed = passed,
            Mismatched = entries.Count - passed,
            Entries = entries
        };
    }

    private static Av3KillReportEntry CompareMarker(Av3FaultPoint marker, Func<Av3WritePlan> planFactory)
    {
        using var storage = new Av3TestStorage();
        var simulated = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(planFactory(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario { FaultPoint = marker }));

        var actual = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = marker });
        if (!actual.MarkerReached)
        {
            return new Av3KillReportEntry
            {
                Marker = marker,
                Simulated = simulated.Classification,
                Actual = actual.Classification,
                Match = false,
                CompareOutcome = Av3RepairClassification.ManualReviewRequired.ToString()
            };
        }

        var match = simulated.Classification == actual.Classification;
        var compare = match ? "match" : Av3RepairClassification.ManualReviewRequired.ToString();
        return new Av3KillReportEntry
        {
            Marker = marker,
            Simulated = simulated.Classification,
            Actual = actual.Classification,
            Match = match,
            CompareOutcome = compare
        };
    }
}