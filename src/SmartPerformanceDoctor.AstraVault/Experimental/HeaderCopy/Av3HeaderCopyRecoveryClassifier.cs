using SmartPerformanceDoctor.AstraVault.FaultInjection;

namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

public static class Av3HeaderCopyRecoveryClassifier
{
    public static Av3RecoveryClassification Classify(Av3HeaderCopyDurabilityState state, bool activationAuthenticated)
    {
        if (state.UnauthenticatedHighGeneration)
        {
            return Av3RecoveryClassification.PreviousGenerationOpen;
        }

        if (state.Copy0ConflictsWithCopy1 || state.Copy1ConflictsWithCopy2)
        {
            return Av3RecoveryClassification.CorruptBlocked;
        }

        if (!activationAuthenticated)
        {
            return state.DurableCount > 0
                ? Av3RecoveryClassification.RecoveryRequired
                : Av3RecoveryClassification.PreviousGenerationOpen;
        }

        return state.DurableCount switch
        {
            0 => Av3RecoveryClassification.PreviousGenerationOpen,
            1 => Av3RecoveryClassification.RedundancyDegraded,
            >= 2 when !state.Copy1ConflictsWithCopy2 => Av3RecoveryClassification.NewGenerationOpen,
            _ => Av3RecoveryClassification.CorruptBlocked
        };
    }
}