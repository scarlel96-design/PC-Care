using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 rollback anchor contract (harness implementation; production route fail-closed).</summary>
public interface IAv3RollbackAnchor
{
    Task<Av3AnchorSnapshot?> ReadAnchorAsync(string vaultRoot, CancellationToken cancellationToken = default);

    Task<Av3AnchorVerificationResult> VerifyAnchorAsync(
        string vaultRoot,
        ulong observedGeneration,
        ReadOnlyMemory<byte> witnessDigest,
        CancellationToken cancellationToken = default);

    Task<Av3AnchorUpdateResult> PrepareAnchorUpdateAsync(
        Av3AnchorUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Av3AnchorUpdateResult> CommitAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default);

    Task AbortAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default);

    Av3AnchorStatus ClassifyAnchorFailure(Av3AnchorFailureReason reason);
}