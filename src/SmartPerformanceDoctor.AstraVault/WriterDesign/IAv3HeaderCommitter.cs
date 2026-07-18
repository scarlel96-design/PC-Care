namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// 3-copy activation header committer (design). Inputs: three copy plans, activation AEAD blobs.
/// Outputs: redundancy report, repair classification. Failures: copy conflict, torn copy, auth failure.
/// Secrets: VMK/DEK/password never in logs. Logging: copy id + generation + classification.
/// Sync ordered writes per crash-safe plan. Cancellation: before final copy rotation only.
/// Idempotency: none — generation monotonic. Testability: HeaderCopy harness. Production gate: off.
/// </summary>
public interface IAv3HeaderCommitter
{
    ValueTask<Av3HeaderCommitResult> CommitThreeCopyAsync(
        Av3HeaderCommitPlan plan,
        CancellationToken cancellationToken = default);
}

public readonly struct Av3HeaderCommitPlan
{
    public Guid TransactionId { get; init; }

    public ulong TargetGeneration { get; init; }

    public ReadOnlyMemory<byte> ActivationPayloadAead { get; init; }
}

public readonly struct Av3HeaderCommitResult
{
    public bool PostFlushAuthenticated { get; init; }

    public string RepairClassification { get; init; }
}