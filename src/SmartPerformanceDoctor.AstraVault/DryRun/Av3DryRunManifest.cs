namespace SmartPerformanceDoctor.AstraVault.DryRun;

/// <summary>Public-safe dry-run manifest (no paths/secrets).</summary>
public sealed class Av3DryRunManifest
{
    public string Scope { get; init; } = "av3_dry_run_test_only";

    public string FixtureKind { get; init; } = "standard";

    public ulong PreviousGeneration { get; init; }

    public ulong TargetGeneration { get; init; }

    public bool PipelineCommitted { get; init; }

    public string Classification { get; init; } = "unknown";

    public string Repair { get; init; } = "unknown";

    public bool ReadOnlyRevalidationPassed { get; init; }

    public bool InvariantsPassed { get; init; }

    public bool TelemetryPassed { get; init; }

    public string ToPublicJson() =>
        $"{{\"scope\":\"{Scope}\",\"fixture\":\"{FixtureKind}\",\"prev_gen\":{PreviousGeneration},\"target_gen\":{TargetGeneration},\"committed\":{PipelineCommitted.ToString().ToLowerInvariant()},\"classification\":\"{Classification}\",\"repair\":\"{Repair}\",\"ro_reval\":{ReadOnlyRevalidationPassed.ToString().ToLowerInvariant()},\"invariants\":{InvariantsPassed.ToString().ToLowerInvariant()},\"telemetry\":{TelemetryPassed.ToString().ToLowerInvariant()}}}";
}