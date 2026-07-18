using SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;
using SmartPerformanceDoctor.AstraVault.FaultInjection;

namespace SmartPerformanceDoctor.AstraVault.Repair;

/// <summary>Classifies repair posture without mutating user data (E-3).</summary>
public static class Av3RepairClassifier
{
    public static Av3RepairClassification FromRecovery(Av3RecoveryClassification recovery) =>
        recovery switch
        {
            Av3RecoveryClassification.NewGenerationOpen => Av3RepairClassification.Healthy,
            Av3RecoveryClassification.RedundancyDegraded => Av3RepairClassification.RedundancyDegraded,
            Av3RecoveryClassification.RecoveryRequired => Av3RepairClassification.RepairRequired,
            Av3RecoveryClassification.CorruptBlocked => Av3RepairClassification.CorruptBlocked,
            Av3RecoveryClassification.RollbackSuspected => Av3RepairClassification.RollbackSuspected,
            Av3RecoveryClassification.PreviousGenerationOpen => Av3RepairClassification.RepairRecommended,
            Av3RecoveryClassification.Aborted => Av3RepairClassification.RepairRecommended,
            Av3RecoveryClassification.UnknownFailClosed => Av3RepairClassification.ManualReviewRequired,
            _ => Av3RepairClassification.ManualReviewRequired
        };

    public static Av3RepairClassification FromHeaderCopyState(
        Av3HeaderCopyDurabilityState state,
        bool activationAuthenticated)
    {
        var recovery = Av3HeaderCopyRecoveryClassifier.Classify(state, activationAuthenticated);
        return FromRecovery(recovery);
    }
}