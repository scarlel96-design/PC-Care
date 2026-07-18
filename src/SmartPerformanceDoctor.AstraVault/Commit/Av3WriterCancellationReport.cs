namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Public-safe cancellation summary (no paths/secrets).</summary>
public sealed class Av3WriterCancellationReport
{
    public Av3CommitPipelineStep? CancelledAtStep { get; init; }

    public string ToPublicSummary() =>
        CancelledAtStep is null ? "cancelled" : $"cancelled_at_{CancelledAtStep}";
}