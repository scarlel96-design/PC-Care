namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>External witness candidate (stub server only; digest-only contract).</summary>
public sealed class Av3ExternalWitnessTrustedAnchorProvider : IAv3TrustedAnchorProvider
{
    private readonly Av3ExternalWitnessStubServer _stub = new();

    public Av3ExternalWitnessStubServer Stub => _stub;

    public Av3TrustedAnchorProviderKind ProviderKind => Av3TrustedAnchorProviderKind.ExternalWitnessCandidate;

    public bool IsAvailableForProductionEnable => true;

    public Task<Av3TrustedAnchorWitness?> ReadWitnessAsync(string vaultRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Av3TrustedAnchorHarnessScope.EnsureE13Root(vaultRoot);
        return Task.FromResult<Av3TrustedAnchorWitness?>(null);
    }

    public Task<Av3TrustedAnchorVerification> VerifyWitnessAsync(
        Av3TrustedAnchorRequest context,
        ulong observedVaultGeneration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Av3TrustedAnchorHarnessScope.Ensure(context.VaultRoot, context.TestHarnessInvocation);
        var response = _stub.Query(
            context.VaultRoot,
            new Av3ExternalWitnessStubContract.WitnessRequest
            {
                VaultId = context.VaultId,
                ObservedGeneration = observedVaultGeneration,
                HeaderRootDigestHex = context.HeaderRootDigestHex,
                CurrentWitnessDigestHex = context.CurrentWitnessDigestHex
            });

        return Task.FromResult(Av3TrustedAnchorClassifier.VerifyExternalWitness(
            observedVaultGeneration,
            response.MonotonicCounter,
            context.CurrentWitnessDigestHex,
            response.WitnessDigestHex,
            response.SignatureValid,
            response.ReplayDetected,
            response.ServerAvailable,
            Av3TrustedAnchorOfflineState.Online));
    }

    public Task<Av3TrustedAnchorCommitResult> PrepareTrustedAnchorUpdateAsync(
        Av3TrustedAnchorRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Av3TrustedAnchorHarnessScope.Ensure(request.VaultRoot, request.TestHarnessInvocation);
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
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Av3TrustedAnchorCommitResult
        {
            Success = true,
            Committed = true,
            FailureReason = Av3TrustedAnchorFailureReason.None,
            PublicErrorClass = "ok"
        });

    public Task AbortTrustedAnchorUpdateAsync(string vaultRoot, Guid updateId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}