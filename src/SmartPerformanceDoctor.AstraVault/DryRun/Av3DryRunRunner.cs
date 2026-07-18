using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

/// <summary>E-8 production-shaped writer dry-run (isolated temp only; NOT production enable).</summary>
public static class Av3DryRunRunner
{
    public static async Task<Av3DryRunResult> RunAsync(
        Av3DryRunOptions options,
        CancellationToken cancellationToken = default)
    {
        Av3DryRunScope.Ensure(options);
        Av3WriterCommitGuard.ClearVaultHarnessState(options.VaultRoot);

        var fixture = Av3SyntheticVaultFixture.Create(options.FixtureKind);
        var plan = Av3DryRunPlan.FromFixture(fixture);
        var harness = plan.ToHarnessOptions(options);

        Av3CommitPipelineRunner.Av3CommitPipelineResult pipeline;
        try
        {
            pipeline = await Av3CommitPipelineRunner.RunHarnessAsync(harness, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Av3WriterCommitGuard.ClearVaultHarnessState(options.VaultRoot);
        }

        var ro = Av3DryRunReadOnlyRevalidator.Revalidate(options.VaultRoot, plan.Crypto.Vmk, pipeline);
        var validation = Av3DryRunValidator.Validate(pipeline, ro);

        var manifest = new Av3DryRunManifest
        {
            FixtureKind = fixture.Kind.ToString(),
            PreviousGeneration = plan.WritePlan.PreviousGeneration,
            TargetGeneration = plan.WritePlan.TargetGeneration,
            PipelineCommitted = pipeline.Committed,
            Classification = pipeline.Classification.ToString(),
            Repair = pipeline.Repair.ToString(),
            ReadOnlyRevalidationPassed = ro.Passed,
            InvariantsPassed = validation.Passed
        };

        var report = new Av3DryRunReport
        {
            Success = pipeline.Committed && validation.Passed && ro.Passed,
            PublicErrorClass = validation.Passed ? "ok" : "av3_dry_run_validation_failed",
            Manifest = manifest,
            TraceSummary = pipeline.Trace.ToPublicSummary(),
            CancellationSummary = pipeline.Cancellation?.ToPublicSummary() ?? string.Empty,
            InvariantSummary = validation.PublicSummary,
            RecoverySummary = $"classification={pipeline.Classification} repair={pipeline.Repair}"
        };

        var telemetry = Av3DryRunTelemetryScanner.ScanDryRunSurfaces(report, pipeline);
        manifest = new Av3DryRunManifest
        {
            Scope = manifest.Scope,
            FixtureKind = manifest.FixtureKind,
            PreviousGeneration = manifest.PreviousGeneration,
            TargetGeneration = manifest.TargetGeneration,
            PipelineCommitted = manifest.PipelineCommitted,
            Classification = manifest.Classification,
            Repair = manifest.Repair,
            ReadOnlyRevalidationPassed = manifest.ReadOnlyRevalidationPassed,
            InvariantsPassed = manifest.InvariantsPassed,
            TelemetryPassed = telemetry.Passed
        };
        report = new Av3DryRunReport
        {
            Success = report.Success && telemetry.Passed,
            PublicErrorClass = report.PublicErrorClass,
            Manifest = manifest,
            TraceSummary = report.TraceSummary,
            CancellationSummary = report.CancellationSummary,
            InvariantSummary = report.InvariantSummary,
            RecoverySummary = report.RecoverySummary
        };

        if (options.RunCleanup && pipeline.Committed)
        {
            _ = Av3CommitHarnessCleanup.TryRunOnce(options.VaultRoot, static () => { });
        }

        return new Av3DryRunResult
        {
            Pipeline = pipeline,
            Report = report,
            ReadOnlyRevalidation = ro,
            Validation = validation,
            Telemetry = telemetry
        };
    }
}