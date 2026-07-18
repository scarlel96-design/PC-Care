using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Fail-closed provider when trusted anchor is unavailable (production enable NO-GO).</summary>
public sealed class Av3NullTrustedAnchorProvider : IAv3TrustedAnchorProvider
{
    public Av3TrustedAnchorProviderKind ProviderKind => Av3TrustedAnchorProviderKind.NullUnavailable;

    public bool IsAvailableForProductionEnable => false;

    public Task<Av3TrustedAnchorWitness?> ReadWitnessAsync(string vaultRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Av3TrustedAnchorWitness?>(null);
    }

    public Task<Av3TrustedAnchorVerification> VerifyWitnessAsync(
        Av3TrustedAnchorRequest context,
        ulong observedVaultGeneration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new Av3TrustedAnchorVerification
        {
            Verified = false,
            AnchorStatus = WriterDesign.Av3AnchorStatus.AnchorUnavailable,
            FailureReason = Av3TrustedAnchorFailureReason.ProviderUnavailable,
            ProductionEnableAllowed = false,
            WriterTrustedPromotionAllowed = false,
            PublicSummary = "trusted_anchor_provider_unavailable"
        });
    }

    public Task<Av3TrustedAnchorCommitResult> PrepareTrustedAnchorUpdateAsync(
        Av3TrustedAnchorRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Av3PhaseGate.ProductionAnchorImplemented && !request.TestHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorAnchorProductionDisabled);
        }

        return Task.FromResult(Fail(Av3TrustedAnchorFailureReason.ProviderUnavailable, "trusted_anchor_provider_unavailable"));
    }

    public Task<Av3TrustedAnchorCommitResult> CommitTrustedAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Fail(Av3TrustedAnchorFailureReason.ProviderUnavailable, "trusted_anchor_provider_unavailable"));

    public Task AbortTrustedAnchorUpdateAsync(string vaultRoot, Guid updateId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static Av3TrustedAnchorCommitResult Fail(Av3TrustedAnchorFailureReason reason, string code) =>
        new()
        {
            Success = false,
            Committed = false,
            FailureReason = reason,
            PublicErrorClass = code
        };
}