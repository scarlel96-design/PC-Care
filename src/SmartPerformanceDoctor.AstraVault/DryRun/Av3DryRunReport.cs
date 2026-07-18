namespace SmartPerformanceDoctor.AstraVault.DryRun;

public sealed class Av3DryRunReport
{
    public bool Success { get; init; }

    public string PublicErrorClass { get; init; } = "ok";

    public Av3DryRunManifest Manifest { get; init; } = new();

    public string TraceSummary { get; init; } = string.Empty;

    public string CancellationSummary { get; init; } = string.Empty;

    public string InvariantSummary { get; init; } = "invariant_ok";

    public string RecoverySummary { get; init; } = string.Empty;

    public string ToPublicSummary() =>
        Success
            ? $"dry_run_ok classification={Manifest.Classification} committed={Manifest.PipelineCommitted}"
            : $"dry_run_fail class={PublicErrorClass}";
}