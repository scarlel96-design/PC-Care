namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Hybrid machine-local + external witness policy (production target design; harness only).</summary>
public sealed class Av3HybridTrustedAnchorPolicyCoordinator
{
    private readonly Av3MachineLocalTrustedAnchorProvider _machine = new();
    private readonly Av3ExternalWitnessTrustedAnchorProvider _external = new();

    public Av3ExternalWitnessTrustedAnchorProvider External => _external;

    public Av3TrustedAnchorProviderKind ProviderKind => Av3TrustedAnchorProviderKind.HybridPolicyCoordinator;

    public bool IsAvailableForProductionEnable =>
        _machine.IsAvailableForProductionEnable && _external.IsAvailableForProductionEnable;

    public async Task<Av3TrustedAnchorVerification> VerifyHybridAsync(
        Av3TrustedAnchorRequest context,
        ulong observedVaultGeneration,
        Av3TrustedAnchorOfflineState offlineState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var machine = await _machine.VerifyWitnessAsync(context, observedVaultGeneration, cancellationToken).ConfigureAwait(false);
        if (!machine.Verified)
        {
            return machine;
        }

        var external = await _external.VerifyWitnessAsync(context, observedVaultGeneration, cancellationToken).ConfigureAwait(false);
        if (!external.Verified)
        {
            return external;
        }

        if (!Av3TrustedAnchorOfflinePolicy.AllowsWriterTrustedPromotion(offlineState))
        {
            return new Av3TrustedAnchorVerification
            {
                Verified = true,
                AnchorStatus = WriterDesign.Av3AnchorStatus.AnchorFresh,
                FailureReason = Av3TrustedAnchorFailureReason.OfflineGraceWriterPromotionDenied,
                WriterTrustedPromotionAllowed = false,
                ProductionEnableAllowed = false,
                PublicSummary = "trusted_hybrid_offline_no_writer_promotion"
            };
        }

        return new Av3TrustedAnchorVerification
        {
            Verified = true,
            AnchorStatus = WriterDesign.Av3AnchorStatus.AnchorFresh,
            FailureReason = Av3TrustedAnchorFailureReason.None,
            WriterTrustedPromotionAllowed = true,
            ProductionEnableAllowed = false,
            PublicSummary = "trusted_hybrid_witness_fresh"
        };
    }

    public static bool SameDiskOnlyClosureDenied() =>
        Av3TrustedAnchorPolicy.SameDiskLocalCannotCloseFullVaultRollback;
}