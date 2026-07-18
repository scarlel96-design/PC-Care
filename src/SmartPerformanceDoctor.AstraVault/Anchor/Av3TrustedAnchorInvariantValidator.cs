using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted anchor invariants linked to writer/anchor phase gates.</summary>
public static class Av3TrustedAnchorInvariantValidator
{
    public static Av3TrustedAnchorInvariantReport ValidatePhaseGates()
    {
#pragma warning disable CS0162 // Gate sentinels must remain executable when flags flip at sign-off
        var violations = new List<Av3TrustedAnchorInvariantViolation>();

        if (!Av3TrustedAnchorPolicy.FullVaultRollbackRequiresExternalOrHybridWitness)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorRequiredForFullRollbackClosure, "trusted_anchor_not_required"));
        }

        if (!Av3TrustedAnchorPolicy.SameDiskLocalCannotCloseFullVaultRollback)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.SameDiskAnchorCannotCloseFullRollback, "same_disk_closure_allowed"));
        }

        foreach (var kind in Enum.GetValues<Av3TrustedAnchorProviderKind>())
        {
            if (kind == Av3TrustedAnchorProviderKind.SameDiskLocalUntrusted
                && Av3TrustedAnchorClassifier.SameDiskAnchorCanCloseFullVaultRollback(kind))
            {
                violations.Add(Code(Av3TrustedAnchorInvariant.SameDiskAnchorCannotCloseFullRollback, "same_disk_marked_closed"));
            }
        }

        if (Av3TrustedAnchorPolicy.StoresSecrets || Av3TrustedAnchorPolicy.StoresPathsOrFilenames)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorNoSecretLeak, "trusted_anchor_stores_secrets_or_paths"));
        }

        if (!Av3TrustedAnchorPrivacyPolicy.ExternalWitnessDigestOnly
            || !Av3TrustedAnchorPrivacyPolicy.NoPasswordInWitness
            || !Av3TrustedAnchorPrivacyPolicy.NoVmkOrDekInWitness)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorNoSecretLeak, "trusted_anchor_privacy_policy_open"));
        }

        if (Av3PhaseGate.ProductionAnchorImplemented && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.ProductionAnchorNotMarkedImplementedWithoutSignoff, "production_anchor_implemented_without_signoff"));
        }

        if (!Av3PhaseGate.ProductionEnableAuthorized
            && (Av3PhaseGate.ProductionWriterEnabled || Av3PhaseGate.WriterEnableReady))
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorUnavailableNoProductionEnable, "production_enable_gate_open"));
        }

        if (Av3PhaseGate.MigrationEnabled && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorUnavailableNoProductionEnable, "migration_enabled"));
        }

        if (Av3TrustedAnchorRecoveryPolicy.AutomaticRepairEnabled)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.FullVaultRollbackDetectedRequiresRecovery, "automatic_repair_enabled"));
        }

        if (!Av3TrustedAnchorOfflinePolicy.WriterTrustedPromotionRequiresOnlineExternalConfirmation)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorOfflineModeNoWriterPromotion, "offline_writer_promotion_allowed"));
        }

        if (!Av3AnchorInvariantValidator.InvariantExpectProductionAnchorDisabled()
            || !Av3AnchorInvariantValidator.InvariantExpectHarnessOnlyRoute()
            || !Av3AnchorInvariantValidator.InvariantExpectProductionEnableUnauthorized())
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorUnavailableNoProductionEnable, "e11_anchor_gates_failed"));
        }

        var writerGates = Av3WriterInvariantValidator.ValidateDisabledProductionGates();
        if (!writerGates.Passed)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.TrustedAnchorUnavailableNoProductionEnable, "writer_gates_failed"));
        }

        if (Av3TrustedAnchorRuntimePolicy.ProductionTrustedAnchorRouteEnabled
            && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.ProductionAnchorNotMarkedImplementedWithoutSignoff, "trusted_production_route_enabled"));
        }

        return Report(violations);
#pragma warning restore CS0162
    }

    public static Av3TrustedAnchorInvariantReport ValidatePublicSurface(string? text, string channel)
    {
        if (Av3TrustedAnchorPublicSurface.IsPublicTextSafe(text))
        {
            return Report([]);
        }

        return Report([Code(Av3TrustedAnchorInvariant.TrustedAnchorPublicErrorSafe, $"trusted_anchor_surface_leak_{channel}")]);
    }

    public static Av3TrustedAnchorInvariantReport ValidateExternalWitnessPosture(Av3TrustedAnchorVerification verification)
    {
        var violations = new List<Av3TrustedAnchorInvariantViolation>();
        violations.AddRange(ValidatePhaseGates().Violations);

        if (verification.FailureReason == Av3TrustedAnchorFailureReason.ExternalWitnessReplayDetected)
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.ExternalWitnessReplayRejected, "replay_not_rejected"));
        }

        if (verification.FullVaultRollbackSuspected && !Av3TrustedAnchorRecoveryPolicy.RequiresRecovery(verification))
        {
            violations.Add(Code(Av3TrustedAnchorInvariant.FullVaultRollbackDetectedRequiresRecovery, "rollback_without_recovery"));
        }

        return Report(violations);
    }

    private static Av3TrustedAnchorInvariantViolation Code(Av3TrustedAnchorInvariant invariant, string code) =>
        new() { Invariant = invariant, PublicCode = code };

    private static Av3TrustedAnchorInvariantReport Report(List<Av3TrustedAnchorInvariantViolation> violations) =>
        new() { Passed = violations.Count == 0, Violations = violations };
}