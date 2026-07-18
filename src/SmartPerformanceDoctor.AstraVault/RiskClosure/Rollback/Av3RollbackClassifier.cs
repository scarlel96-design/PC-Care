using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Rollback;

/// <summary>R10 generation rollback closure (no data mutation).</summary>
public static class Av3RollbackClassifier
{
    public static Av3RecoveryClassification Classify(Av3RollbackEvidence evidence)
    {
        if (evidence.EqualGenerationConflictingRoot)
        {
            return Av3RecoveryClassification.CorruptBlocked;
        }

        if (evidence.PreviousRootDigestMismatch || evidence.OldMetadataRootReplay)
        {
            return Av3RecoveryClassification.CorruptBlocked;
        }

        if (evidence.JournalGenerationWindowInvalid)
        {
            return Av3RecoveryClassification.RollbackSuspected;
        }

        if (Av3GenerationWindowPolicy.IsLocalRollback(evidence.LastAuthenticatedGeneration, evidence.ObservedHeaderGeneration)
            || Av3GenerationWindowPolicy.IsLocalRollback(evidence.LastAuthenticatedGeneration, evidence.ObservedMetadataGeneration)
            || Av3GenerationWindowPolicy.IsLocalRollback(evidence.LastAuthenticatedGeneration, evidence.ObservedJournalTargetGeneration))
        {
            return Av3RecoveryClassification.RollbackSuspected;
        }

        if (evidence.HeaderMetadataGenerationMismatch)
        {
            return Av3RecoveryClassification.CorruptBlocked;
        }

        if (evidence.StaleActivationPayload || evidence.StaleHighGenerationUnauthenticated)
        {
            return evidence.ActivationAuthenticated
                ? Av3RecoveryClassification.UnknownFailClosed
                : Av3RecoveryClassification.PreviousGenerationOpen;
        }

        if (evidence.JournalClaimsForwardCommit && !evidence.ActivationAuthenticated)
        {
            return Av3RecoveryClassification.RollbackSuspected;
        }

        if (evidence.FullVaultRollbackWithoutAnchorSuspected && !Av3AnchorPolicy.HasTrustedAnchor(false, false))
        {
            return Av3RecoveryClassification.UnknownFailClosed;
        }

        if (evidence.ObservedHeaderGeneration > evidence.LastAuthenticatedGeneration && !evidence.ActivationAuthenticated)
        {
            return Av3RecoveryClassification.PreviousGenerationOpen;
        }

        return Av3RecoveryClassification.NewGenerationOpen;
    }

    public static Av3RepairClassification RepairPosture(Av3RollbackEvidence evidence)
    {
        var recovery = Classify(evidence);
        if (recovery == Av3RecoveryClassification.UnknownFailClosed
            && (evidence.StaleActivationPayload || evidence.FullVaultRollbackWithoutAnchorSuspected))
        {
            return Av3RepairClassification.ManualReviewRequired;
        }

        return Av3RepairClassifier.FromRecovery(recovery);
    }
}