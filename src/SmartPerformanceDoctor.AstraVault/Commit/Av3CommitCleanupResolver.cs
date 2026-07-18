using SmartPerformanceDoctor.AstraVault.FaultInjection;

namespace SmartPerformanceDoctor.AstraVault.Commit;

internal static class Av3CommitCleanupResolver
{
    internal static Av3CommitCleanupPosture Resolve(Av3CommitSnapshot snapshot, bool postAuthTrusted)
    {
        if (!postAuthTrusted)
        {
            return Av3CommitCleanupPosture.NotApplicable;
        }

        if (snapshot.CleanupCompleted && !snapshot.CleanupFailed)
        {
            return Av3CommitCleanupPosture.Completed;
        }

        if (!snapshot.CleanupFailed)
        {
            return Av3CommitCleanupPosture.NotApplicable;
        }

        if (snapshot.RedundancyDegraded || snapshot.HeaderCopyDurableCount == 1)
        {
            return Av3CommitCleanupPosture.RedundancyDegradedCleanupRequired;
        }

        return Av3CommitCleanupPosture.NewGenerationOpenCleanupRequired;
    }
}