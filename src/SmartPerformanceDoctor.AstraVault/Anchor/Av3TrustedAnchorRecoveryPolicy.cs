using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Recovery posture when machine binding or witness diverges (no automatic repair).</summary>
public static class Av3TrustedAnchorRecoveryPolicy
{
    public static bool AutomaticRepairEnabled =>
        Av3TrustedAnchorPolicy.ProductionAnchorImplemented && Av3PhaseGate.MigrationEnabled;

    public const bool FullVaultRollbackRequiresExplicitRecovery = true;

    public static bool RequiresRecovery(Av3TrustedAnchorVerification verification) =>
        verification.FullVaultRollbackSuspected
        || verification.FailureReason is Av3TrustedAnchorFailureReason.RecoveryRequired
            or Av3TrustedAnchorFailureReason.MachineBindingMismatch
            or Av3TrustedAnchorFailureReason.HeaderCommitFailedRecoveryRequired
            or Av3TrustedAnchorFailureReason.ExternalWitnessCounterRollback
            or Av3TrustedAnchorFailureReason.ExternalWitnessDigestMismatch;
}