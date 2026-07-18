namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// Durable byte I/O abstraction (design). Inputs: relative paths, spans, flush boundaries.
/// Outputs: durable write handles, flush results. Failures: IO, partial flush, unlock during write.
/// Secrets: never logged. Logging: generation + public error class only. Sync preferred; async optional with same ordering.
/// Cancellation: cooperative abort before activation commit; prior generation remains trusted. Idempotency: none for commit; temp files use transaction id.
/// Testability: harness implements on isolated roots. Production gate: off until FI + review pass.
/// </summary>
public interface IAv3DurableStore
{
    ValueTask<Av3DurableStoreWriteResult> WriteTempThenCommitAsync(
        string relativePath,
        ReadOnlyMemory<byte> payload,
        Av3DurableCommitOptions options,
        CancellationToken cancellationToken = default);

    ValueTask FlushDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken = default);
}

public readonly struct Av3DurableCommitOptions
{
    public Guid TransactionId { get; init; }

    public ulong TargetGeneration { get; init; }
}

public readonly struct Av3DurableStoreWriteResult
{
    public bool Durable { get; init; }

    public string PublicErrorClass { get; init; }
}