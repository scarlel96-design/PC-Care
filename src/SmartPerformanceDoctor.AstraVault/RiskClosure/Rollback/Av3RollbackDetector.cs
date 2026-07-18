using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Rollback;

/// <summary>R10: builds evidence and classifies local rollback / consistency violations.</summary>
public static class Av3RollbackDetector
{
    public static Av3RollbackEvidence BuildEvidence(Av3RollbackObservation observation) =>
        new()
        {
            LastAuthenticatedGeneration = observation.LastAuthenticatedGeneration,
            ObservedHeaderGeneration = observation.ObservedHeaderGeneration,
            ObservedMetadataGeneration = observation.ObservedMetadataGeneration,
            ObservedJournalPreviousGeneration = observation.ObservedJournalPreviousGeneration,
            ObservedJournalTargetGeneration = observation.ObservedJournalTargetGeneration,
            HeaderMetadataGenerationMismatch = observation.ObservedHeaderGeneration != observation.ObservedMetadataGeneration,
            JournalGenerationWindowInvalid = !Av3GenerationWindowPolicy.IsForwardJournalWindow(
                observation.ObservedJournalPreviousGeneration,
                observation.ObservedJournalTargetGeneration),
            EqualGenerationConflictingRoot = observation.EqualGenerationConflictingRoot,
            PreviousRootDigestMismatch = observation.PreviousRootDigestMismatch,
            StaleHighGenerationUnauthenticated = Av3GenerationWindowPolicy.IsStaleHighGeneration(
                observation.LastAuthenticatedGeneration,
                Math.Max(observation.ObservedHeaderGeneration, observation.ObservedMetadataGeneration),
                observation.ActivationAuthenticated),
            StaleActivationPayload = observation.StaleActivationPayload,
            OldMetadataRootReplay = observation.OldMetadataRootReplay,
            JournalClaimsForwardCommit = observation.JournalClaimsForwardCommit,
            ActivationAuthenticated = observation.ActivationAuthenticated,
            FullVaultRollbackWithoutAnchorSuspected = observation.FullVaultRollbackWithoutAnchorSuspected
        };

    public static Av3RecoveryClassification Classify(Av3RollbackObservation observation) =>
        Av3RollbackClassifier.Classify(BuildEvidence(observation));

    public static Av3RepairClassification RepairPosture(Av3RollbackObservation observation) =>
        Av3RollbackClassifier.RepairPosture(BuildEvidence(observation));
}