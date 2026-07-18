using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.DryRun;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 harness bridge: header commit outcome gates trusted anchor promotion.</summary>
public static class Av3TrustedAnchorCommitBridge
{
    public sealed class TrustedAnchorBridgeResult
    {
        public bool HeaderCommitted { get; init; }

        public bool TrustedAnchorCommitted { get; init; }

        public bool TrustedGenerationPromoted { get; init; }

        public Av3TrustedAnchorFailureReason FailureReason { get; init; }

        public string PublicSummary { get; init; } = string.Empty;
    }

    public static async Task<TrustedAnchorBridgeResult> RunTrustedAnchorAfterHeaderAsync(
        Av3DryRunOptions options,
        Av3CommitPipelineRunner.Av3CommitPipelineResult pipeline,
        Av3TrustedAnchorRequest anchorRequest,
        IAv3TrustedAnchorProvider provider,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.TestHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorHarnessOnly);
        }

        Av3TrustedAnchorHarnessScope.Ensure(options.VaultRoot, options.TestHarnessInvocation);

        if (!pipeline.Committed)
        {
            return new TrustedAnchorBridgeResult
            {
                HeaderCommitted = false,
                TrustedAnchorCommitted = false,
                TrustedGenerationPromoted = false,
                FailureReason = Av3TrustedAnchorFailureReason.HeaderCommitFailedRecoveryRequired,
                PublicSummary = "trusted_header_not_committed"
            };
        }

        var prep = await provider.PrepareTrustedAnchorUpdateAsync(anchorRequest, cancellationToken).ConfigureAwait(false);
        if (!prep.Success)
        {
            return new TrustedAnchorBridgeResult
            {
                HeaderCommitted = true,
                TrustedAnchorCommitted = false,
                TrustedGenerationPromoted = false,
                FailureReason = prep.FailureReason,
                PublicSummary = prep.PublicErrorClass
            };
        }

        var commit = await provider.CommitTrustedAnchorUpdateAsync(anchorRequest.VaultRoot, anchorRequest.UpdateId, cancellationToken).ConfigureAwait(false);
        var promoted = commit.Success && commit.Committed && pipeline.PostAuthDataTrusted;
        return new TrustedAnchorBridgeResult
        {
            HeaderCommitted = true,
            TrustedAnchorCommitted = commit.Committed,
            TrustedGenerationPromoted = promoted,
            FailureReason = promoted ? Av3TrustedAnchorFailureReason.None : Av3TrustedAnchorFailureReason.TrustedAnchorUpdateNotCommitted,
            PublicSummary = promoted ? "trusted_anchor_promoted" : commit.PublicErrorClass
        };
    }

    public static TrustedAnchorBridgeResult EvaluateHeaderSuccessAnchorFailed()
    {
        return new TrustedAnchorBridgeResult
        {
            HeaderCommitted = true,
            TrustedAnchorCommitted = false,
            TrustedGenerationPromoted = false,
            FailureReason = Av3TrustedAnchorFailureReason.TrustedAnchorUpdateNotCommitted,
            PublicSummary = "trusted_anchor_commit_failed"
        };
    }

    public static TrustedAnchorBridgeResult EvaluateAnchorSuccessHeaderFailed()
    {
        return new TrustedAnchorBridgeResult
        {
            HeaderCommitted = false,
            TrustedAnchorCommitted = true,
            TrustedGenerationPromoted = false,
            FailureReason = Av3TrustedAnchorFailureReason.HeaderCommitFailedRecoveryRequired,
            PublicSummary = "trusted_header_commit_failed_recovery_required"
        };
    }
}