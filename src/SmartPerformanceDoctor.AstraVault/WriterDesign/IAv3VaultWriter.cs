namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// Top-level production vault writer entry (design only — no implementation in E-5).
/// Inputs: vault root path, unlock proof, write intents (object/metadata deltas).
/// Outputs: committed generation or structured failure. Failures: policy, crypto, durability, rollback, anchor.
/// Secrets: password/VMK/DEK never stored or logged. Logging: public error taxonomy per writer gate §5.
/// Async primary API; sync wrappers optional. Cancellation before activation commit. Idempotency: new transaction per commit attempt.
/// Testability: substitute all sub-interfaces. Production enable: external review + checklist + FI — all NO-GO in E-5.
/// </summary>
public interface IAv3VaultWriter
{
    ValueTask<Av3VaultCommitResult> CommitAsync(
        Av3VaultCommitRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<IAv3WriteSession> OpenWriteSessionAsync(
        Av3WriteSessionOpenRequest request,
        CancellationToken cancellationToken = default);
}

public readonly struct Av3WriteSessionOpenRequest
{
    public string VaultRootPath { get; init; }

    public ulong TrustedGeneration { get; init; }
}

public readonly struct Av3VaultCommitRequest
{
    public Guid TransactionId { get; init; }

    public ulong TargetGeneration { get; init; }
}

public readonly struct Av3VaultCommitResult
{
    public bool Committed { get; init; }

    public ulong TrustedGeneration { get; init; }

    public string Classification { get; init; }
}