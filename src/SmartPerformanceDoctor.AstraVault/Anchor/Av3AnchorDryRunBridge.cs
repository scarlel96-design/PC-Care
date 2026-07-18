using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.DryRun;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 bridge: post-commit anchor update for committed E-11 dry-run/harness roots.</summary>
public static class Av3AnchorDryRunBridge
{
    public sealed class Av3AnchorDryRunBridgeResult
    {
        public Av3CommitPipelineRunner.Av3CommitPipelineResult Pipeline { get; init; } = new();

        public bool AnchorUpdated { get; init; }

        public bool AnchorPostureCommitted { get; init; }

        public Av3AnchorStatus AnchorStatus { get; init; }

        public string PublicSummary { get; init; } = string.Empty;
    }

    public static async Task<Av3AnchorDryRunBridgeResult> RunAnchorAfterDryRunAsync(
        Av3DryRunOptions options,
        Av3CommitPipelineRunner.Av3CommitPipelineResult pipeline,
        Guid containerId,
        ReadOnlyMemory<byte> witnessDigest,
        IAv3RollbackAnchor? anchor = null,
        CancellationToken cancellationToken = default)
    {
        anchor ??= new Av3HarnessRollbackAnchor();

        if (!pipeline.Committed || !Av3AnchorHarnessScope.IsE11RootAllowed(options.VaultRoot, out _))
        {
            return new Av3AnchorDryRunBridgeResult
            {
                Pipeline = pipeline,
                AnchorUpdated = false,
                AnchorPostureCommitted = pipeline.Committed,
                AnchorStatus = Av3AnchorStatus.AnchorUnavailable,
                PublicSummary = "anchor_bridge_skipped"
            };
        }

        if (!pipeline.PostAuthDataTrusted
            || !pipeline.Snapshot.ActivationAuthenticated
            || !pipeline.Snapshot.MetadataAuthenticated)
        {
            return AnchorFail(pipeline, Av3AnchorStatus.AnchorRecoveryRequired, "anchor_bridge_pre_commit_posture");
        }

        var updateId = Guid.NewGuid();
        var request = new Av3AnchorUpdateRequest
        {
            VaultRoot = options.VaultRoot,
            TestHarnessInvocation = options.TestHarnessInvocation,
            ContainerId = containerId,
            TargetGeneration = pipeline.Snapshot.AttemptedTargetGeneration,
            WitnessDigest = witnessDigest,
            UpdateId = updateId
        };

        try
        {
            var prepared = await anchor.PrepareAnchorUpdateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!prepared.Success)
            {
                return AnchorFail(pipeline, anchor.ClassifyAnchorFailure(prepared.FailureReason), prepared.PublicErrorClass);
            }

            var committed = await anchor.CommitAnchorUpdateAsync(options.VaultRoot, updateId, cancellationToken)
                .ConfigureAwait(false);
            if (!committed.Success)
            {
                await anchor.AbortAnchorUpdateAsync(options.VaultRoot, updateId, cancellationToken)
                    .ConfigureAwait(false);
                return AnchorFail(pipeline, anchor.ClassifyAnchorFailure(committed.FailureReason), committed.PublicErrorClass);
            }

            var verify = await anchor.VerifyAnchorAsync(
                    options.VaultRoot,
                    pipeline.Snapshot.AttemptedTargetGeneration,
                    witnessDigest,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!verify.Verified || verify.Status != Av3AnchorStatus.AnchorFresh)
            {
                return AnchorFail(pipeline, verify.Status, verify.PublicSummary);
            }

            return new Av3AnchorDryRunBridgeResult
            {
                Pipeline = pipeline,
                AnchorUpdated = true,
                AnchorPostureCommitted = true,
                AnchorStatus = Av3AnchorStatus.AnchorFresh,
                PublicSummary = Av3AnchorPublicSurface.ToPublicVerificationSummary(verify)
            };
        }
        finally
        {
            Av3HarnessRollbackAnchor.ClearHarnessState(options.VaultRoot);
        }
    }

    private static Av3AnchorDryRunBridgeResult AnchorFail(
        Av3CommitPipelineRunner.Av3CommitPipelineResult pipeline,
        Av3AnchorStatus status,
        string summary) =>
        new()
        {
            Pipeline = WithAnchorPostureNotCommitted(pipeline),
            AnchorUpdated = false,
            AnchorPostureCommitted = false,
            AnchorStatus = status,
            PublicSummary = summary
        };

    private static Av3CommitPipelineRunner.Av3CommitPipelineResult WithAnchorPostureNotCommitted(
        Av3CommitPipelineRunner.Av3CommitPipelineResult pipeline) =>
        new()
        {
            Classification = pipeline.Classification,
            Repair = pipeline.Repair,
            Committed = false,
            PostAuthDataTrusted = pipeline.PostAuthDataTrusted,
            CleanupPosture = pipeline.CleanupPosture,
            Trace = pipeline.Trace,
            Snapshot = pipeline.Snapshot,
            Cancelled = pipeline.Cancelled,
            Cancellation = pipeline.Cancellation
        };
}