namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted monotonic anchor provider contract (production-disabled implementations).</summary>
public interface IAv3TrustedAnchorProvider
{
    Av3TrustedAnchorProviderKind ProviderKind { get; }

    bool IsAvailableForProductionEnable { get; }

    Task<Av3TrustedAnchorWitness?> ReadWitnessAsync(string vaultRoot, CancellationToken cancellationToken = default);

    Task<Av3TrustedAnchorVerification> VerifyWitnessAsync(
        Av3TrustedAnchorRequest context,
        ulong observedVaultGeneration,
        CancellationToken cancellationToken = default);

    Task<Av3TrustedAnchorCommitResult> PrepareTrustedAnchorUpdateAsync(
        Av3TrustedAnchorRequest request,
        CancellationToken cancellationToken = default);

    Task<Av3TrustedAnchorCommitResult> CommitTrustedAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default);

    Task AbortTrustedAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default);
}