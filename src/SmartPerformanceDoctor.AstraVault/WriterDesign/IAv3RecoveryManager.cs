namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// Post-crash / post-abort recovery orchestration (design). Inputs: vault root snapshot, journal + header observations.
/// Outputs: trusted generation, recovery classification, optional anchor status. Failures: corrupt blocked, rollback suspected.
/// Secrets: never emitted in public summary. Logging: classification + generation only.
/// Sync on unlock/recovery paths. Cancellation: N/A for single-shot classify. Idempotency: read-only classify is pure.
/// Testability: FI matrix + rollback harness. Production gate: paired with writer enable.
/// </summary>
public interface IAv3RecoveryManager
{
    Av3RecoveryAssessment AssessAfterInterrupt(Av3RecoveryAssessmentInput input);
}

public readonly struct Av3RecoveryAssessmentInput
{
    public ulong LastAuthenticatedGeneration { get; init; }

    public Av3AnchorStatus? AnchorStatus { get; init; }
}

public readonly struct Av3RecoveryAssessment
{
    public ulong TrustedOpenGeneration { get; init; }

    public string Classification { get; init; }

    public Av3AnchorStatus AnchorStatus { get; init; }
}