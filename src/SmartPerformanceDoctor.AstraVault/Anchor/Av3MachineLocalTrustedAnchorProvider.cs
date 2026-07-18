using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Production-shaped machine-local candidate (test double; no OS protected storage binding).</summary>
public sealed class Av3MachineLocalTrustedAnchorProvider : IAv3TrustedAnchorProvider
{
    public static Av3TrustedAnchorBindingState TestingBindingState { get; set; } = Av3TrustedAnchorBindingState.Bound;

    public Av3TrustedAnchorProviderKind ProviderKind => Av3TrustedAnchorProviderKind.MachineLocalCandidate;

    public bool IsAvailableForProductionEnable => TestingBindingState == Av3TrustedAnchorBindingState.Bound;

    public Task<Av3TrustedAnchorWitness?> ReadWitnessAsync(string vaultRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TestingBindingState == Av3TrustedAnchorBindingState.Unavailable)
        {
            return Task.FromResult<Av3TrustedAnchorWitness?>(null);
        }

        return Task.FromResult<Av3TrustedAnchorWitness?>(new Av3TrustedAnchorWitness
        {
            VaultId = Guid.Empty,
            AnchorId = Guid.Empty,
            Generation = 1,
            MonotonicCounter = 1,
            CurrentWitnessDigestHex = "00",
            ProviderKind = ProviderKind,
            MachineBindingState = TestingBindingState,
            ExternalWitnessState = Av3TrustedAnchorExternalState.Unknown,
            OfflineGraceState = Av3TrustedAnchorOfflineState.Online,
            RecoveryState = TestingBindingState == Av3TrustedAnchorBindingState.RecoveryRequired
                ? Av3TrustedAnchorRecoveryState.RecoveryRequired
                : Av3TrustedAnchorRecoveryState.None
        });
    }

    public Task<Av3TrustedAnchorVerification> VerifyWitnessAsync(
        Av3TrustedAnchorRequest context,
        ulong observedVaultGeneration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ClassifyBinding());
    }

    public Task<Av3TrustedAnchorCommitResult> PrepareTrustedAnchorUpdateAsync(
        Av3TrustedAnchorRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.TestHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorHarnessOnly);
        }

        var binding = ClassifyBinding();
        if (!binding.Verified)
        {
            return Task.FromResult(new Av3TrustedAnchorCommitResult
            {
                Success = false,
                Committed = false,
                FailureReason = binding.FailureReason,
                PublicErrorClass = binding.PublicSummary
            });
        }

        return Task.FromResult(new Av3TrustedAnchorCommitResult
        {
            Success = true,
            Committed = false,
            FailureReason = Av3TrustedAnchorFailureReason.None,
            PublicErrorClass = "ok"
        });
    }

    public Task<Av3TrustedAnchorCommitResult> CommitTrustedAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var binding = ClassifyBinding();
        return Task.FromResult(new Av3TrustedAnchorCommitResult
        {
            Success = binding.Verified,
            Committed = binding.Verified,
            FailureReason = binding.FailureReason,
            PublicErrorClass = binding.PublicSummary
        });
    }

    public Task AbortTrustedAnchorUpdateAsync(string vaultRoot, Guid updateId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static Av3TrustedAnchorVerification ClassifyBinding() =>
        TestingBindingState switch
        {
            Av3TrustedAnchorBindingState.Bound => new Av3TrustedAnchorVerification
            {
                Verified = true,
                AnchorStatus = WriterDesign.Av3AnchorStatus.AnchorFresh,
                FailureReason = Av3TrustedAnchorFailureReason.None,
                ProductionEnableAllowed = false,
                WriterTrustedPromotionAllowed = false,
                PublicSummary = "trusted_machine_binding_bound"
            },
            Av3TrustedAnchorBindingState.Mismatch or Av3TrustedAnchorBindingState.RecoveryRequired => new Av3TrustedAnchorVerification
            {
                Verified = false,
                AnchorStatus = WriterDesign.Av3AnchorStatus.AnchorRecoveryRequired,
                FailureReason = Av3TrustedAnchorFailureReason.MachineBindingMismatch,
                ProductionEnableAllowed = false,
                WriterTrustedPromotionAllowed = false,
                PublicSummary = "trusted_machine_binding_recovery_required"
            },
            _ => new Av3TrustedAnchorVerification
            {
                Verified = false,
                AnchorStatus = WriterDesign.Av3AnchorStatus.AnchorUnavailable,
                FailureReason = Av3TrustedAnchorFailureReason.MachineBindingUnavailable,
                ProductionEnableAllowed = false,
                WriterTrustedPromotionAllowed = false,
                PublicSummary = "trusted_machine_binding_unavailable"
            }
        };
}