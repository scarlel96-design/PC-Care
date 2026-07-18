namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// Journal durable recorder (design). Inputs: digest-only descriptor, state transitions. Outputs: durable JNAL bytes.
/// Failures: torn journal, confidentiality violation, generation mismatch. No paths/passwords/VMK in journal.
/// Logging: transaction id + state enum. Sync commit before activation. Cancellation: abort leaves journal non-authoritative.
/// Idempotency: replay-safe only after full authentication. Testability: harness + R11 scanners. Production gate: JournalWriterEnabled false.
/// </summary>
public interface IAv3JournalRecorder
{
    ValueTask<Av3JournalRecordResult> RecordStateAsync(
        Av3JournalRecordRequest request,
        CancellationToken cancellationToken = default);
}

public readonly struct Av3JournalRecordRequest
{
    public Guid TransactionId { get; init; }

    public ulong PreviousGeneration { get; init; }

    public ulong TargetGeneration { get; init; }

    public ReadOnlyMemory<byte> TargetMetadataRootCiphertextDigest { get; init; }
}

public readonly struct Av3JournalRecordResult
{
    public bool Durable { get; init; }

    public bool ConfidentialityPassed { get; init; }
}