using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3CommitRecoveryManager : IAv3RecoveryManager
{
    /// <summary>E-7: recovery manager performs assessment/classification only — no auto-repair.</summary>
    public const bool PerformsAutomaticRepair = false;
    public Av3RecoveryAssessment AssessAfterInterrupt(Av3RecoveryAssessmentInput input) =>
        AssessSnapshot(
            new Av3CommitSnapshot { PreviousAuthenticatedGeneration = input.LastAuthenticatedGeneration },
            input.AnchorStatus);

    internal (Av3RecoveryClassification Recovery, Av3RepairClassification Repair) ClassifySnapshot(Av3CommitSnapshot snapshot)
    {
        var recovery = Av3RecoveryClassifier.Classify(snapshot);
        var repair = Av3RepairClassifier.FromRecovery(recovery);
        return (recovery, repair);
    }

    internal Av3RecoveryAssessment AssessSnapshot(Av3CommitSnapshot snapshot, Av3AnchorStatus? anchorStatus)
    {
        var (recovery, _) = ClassifySnapshot(snapshot);
        return new Av3RecoveryAssessment
        {
            TrustedOpenGeneration = recovery == Av3RecoveryClassification.NewGenerationOpen
                ? snapshot.AttemptedTargetGeneration
                : snapshot.PreviousAuthenticatedGeneration,
            Classification = recovery.ToString(),
            AnchorStatus = anchorStatus ?? Av3AnchorStatus.AnchorUnavailable
        };
    }
}