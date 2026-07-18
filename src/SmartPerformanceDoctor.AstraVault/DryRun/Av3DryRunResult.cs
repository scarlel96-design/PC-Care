using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

public sealed class Av3DryRunResult
{
    public Av3CommitPipelineRunner.Av3CommitPipelineResult Pipeline { get; init; } = new();

    public Av3DryRunReport Report { get; init; } = new();

    public Av3DryRunReadOnlyRevalidation ReadOnlyRevalidation { get; init; } = new();

    public Av3DryRunValidationResult Validation { get; init; } = new();

    public Av3DryRunTelemetryScanResult Telemetry { get; init; } = new();
}