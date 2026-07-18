using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

public sealed class Av3DryRunValidationResult
{
    public bool Passed { get; init; }

    public string PublicSummary { get; init; } = "dry_run_validation_ok";
}

public static class Av3DryRunValidator
{
    public static Av3DryRunValidationResult Validate(
        Av3CommitPipelineRunner.Av3CommitPipelineResult pipeline,
        Av3DryRunReadOnlyRevalidation readOnly)
    {
        if (!Av3WriterInvariantValidator.InvariantExpectWriterGatesClosed())
        {
            return Fail("writer_gates_not_closed");
        }

        if (!DryRunExpectEnableFlagsClosed())
        {
            return Fail("enable_flags_invalid");
        }

        var gateReport = Av3WriterInvariantValidator.ValidateDisabledProductionGates();
        if (!gateReport.Passed)
        {
            return Fail("disabled_gates_invariant");
        }

        var pipelineReport = Av3WriterInvariantValidator.ValidatePipelineResult(pipeline);
        if (!pipelineReport.Passed)
        {
            return Fail("pipeline_invariant");
        }

        var trustedReport = Av3WriterInvariantValidator.ValidateTrustedGenerationPreserved(
            pipeline.Snapshot.PreviousAuthenticatedGeneration,
            pipeline);
        if (!trustedReport.Passed)
        {
            return Fail("trusted_generation");
        }

        if (pipeline.Committed && !readOnly.Passed)
        {
            return Fail("read_only_revalidation");
        }

        return new Av3DryRunValidationResult { Passed = true };
    }

    /// <summary>
    /// Dry-run validation posture. 50.4.0: production may be authorized; dry-run remains valid.
    /// When production is authorized, dry-run is an allowed non-mutating validation path.
    /// </summary>
    internal static bool DryRunExpectEnableFlagsClosed() =>
        Av3PhaseGate.ProductionEnableAuthorized
        || (!Av3PhaseGate.ProductionWriterEnabled
            && !Av3PhaseGate.WriterEnableReady
            && !Av3PhaseGate.JournalWriterEnabled
            && !Av3PhaseGate.MigrationEnabled
            && !Av3PhaseGate.ExternalReviewCompleted);

    private static Av3DryRunValidationResult Fail(string code) =>
        new() { Passed = false, PublicSummary = $"dry_run_validation_fail_{code}" };
}