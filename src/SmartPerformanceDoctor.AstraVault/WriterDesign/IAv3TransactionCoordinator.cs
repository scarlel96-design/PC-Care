namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// Coordinates durable object/metadata/journal/header commit order (design). Inputs: write plan, session keys in memory only.
/// Outputs: commit phase, abort reason. Failures: phase ordering violation, authentication failure.
/// Secrets: session-bound; zeroize on dispose. Logging: phase + transaction id. Async with strict phase barriers.
/// Cancellation: triggers Aborted state; old generation preserved. Idempotency: not across retries without new transaction id.
/// Testability: experimental transaction + FI. Production gate: ProductionWriterEnabled false.
/// </summary>
public interface IAv3TransactionCoordinator
{
    ValueTask<Av3TransactionPhaseResult> AdvancePhaseAsync(
        Av3TransactionPhase phase,
        CancellationToken cancellationToken = default);
}

public enum Av3TransactionPhase
{
    Preparing,
    WritingObjects,
    WritingMetadata,
    WritingJournal,
    Flushing,
    WritingActivationHeader,
    PostFlushReread,
    PostFlushAuthentication
}

public readonly struct Av3TransactionPhaseResult
{
    public Av3TransactionPhase CompletedPhase { get; init; }

    public bool Success { get; init; }

    public string PublicErrorClass { get; init; }
}