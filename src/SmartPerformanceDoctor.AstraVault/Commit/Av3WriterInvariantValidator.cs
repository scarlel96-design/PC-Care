using SmartPerformanceDoctor.AstraVault.Anchor;
using SmartPerformanceDoctor.AstraVault.DiskDurability;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>E-7 invariant checks (gates, pipeline posture, journal policy).</summary>
public static class Av3WriterInvariantValidator
{
    public static Av3WriterInvariantReport ValidateDisabledProductionGates()
    {
        var violations = new List<Av3WriterInvariantViolation>();
        if (!InvariantExpectWriterGatesClosed())
        {
            violations.Add(Code(Av3WriterInvariant.NoProductionRouteWhileDisabled, "writer_journal_migration_gate_open"));
        }

        if (!InvariantExpectNoAutoDeleteDefault())
        {
            violations.Add(Code(Av3WriterInvariant.NoAutoDeleteOriginal, "auto_delete_default"));
        }

        return Report(violations);
    }

    /// <summary>
    /// 50.4.0: when ProductionEnableAuthorized, gates must be fully open consistently.
    /// Otherwise fail-closed (all production routes disabled).
    /// </summary>
    internal static bool InvariantExpectWriterGatesClosed() =>
        Av3PhaseGate.ProductionEnableAuthorized
            ? Av3PhaseGate.ProductionWriterEnabled
              && Av3PhaseGate.JournalWriterEnabled
              && Av3PhaseGate.WriterEnableReady
              && Av3PhaseGate.MigrationEnabled
              && Av3PhaseGate.ExternalReviewCompleted
            : !Av3PhaseGate.ProductionWriterEnabled
              && !Av3PhaseGate.JournalWriterEnabled
              && !Av3PhaseGate.WriterEnableReady
              && !Av3PhaseGate.MigrationEnabled
              && !Av3PhaseGate.ExternalReviewCompleted;

    internal static bool InvariantExpectNoAutoDeleteDefault() =>
        !Av3DefaultWritePolicy.Instance.AllowsUserOriginDeletionByDefault;

    public static Av3WriterInvariantReport ValidatePipelineResult(Av3CommitPipelineRunner.Av3CommitPipelineResult result)
    {
        var violations = new List<Av3WriterInvariantViolation>();

        if (result.Committed && (!result.Snapshot.ActivationAuthenticated || !result.Snapshot.MetadataAuthenticated))
        {
            violations.Add(Code(Av3WriterInvariant.AuthBeforeTrust, "committed_without_auth"));
        }

        if (result.Committed && !result.Snapshot.RereadSucceeded)
        {
            violations.Add(Code(Av3WriterInvariant.PostFlushRereadBeforeAuth, "committed_without_reread"));
        }

        if (result.Committed && result.Snapshot.ActivationFlushed && !result.PostAuthDataTrusted)
        {
            violations.Add(Code(Av3WriterInvariant.VerifyBeforeCommit, "committed_before_verify"));
        }

        if (result.Committed
            && result.Snapshot.PreviousAuthenticatedGeneration == result.Snapshot.AttemptedTargetGeneration
            && result.Classification == Av3RecoveryClassification.NewGenerationOpen)
        {
            violations.Add(Code(Av3WriterInvariant.NoPartialGenerationNormalOpen, "equal_gen_open"));
        }

        if (!result.Committed
            && result.Classification == Av3RecoveryClassification.NewGenerationOpen)
        {
            violations.Add(Code(Av3WriterInvariant.NoPartialGenerationNormalOpen, "uncommitted_new_gen_open"));
        }

        if (result.Snapshot.CleanupFailed && result.Committed)
        {
            violations.Add(Code(Av3WriterInvariant.CleanupFailureSeparated, "committed_with_cleanup_fail"));
        }

        if (result.PostAuthDataTrusted && result.Snapshot.CleanupFailed && result.Committed)
        {
            violations.Add(Code(Av3WriterInvariant.CleanupFailureSeparated, "cleanup_fail_committed"));
        }

        if (result.Snapshot.CleanupFailed
            && !result.Snapshot.CleanupCompleted
            && result.Classification == Av3RecoveryClassification.NewGenerationOpen)
        {
            violations.Add(Code(Av3WriterInvariant.CleanupFailureSeparated, "cleanup_fail_new_gen_open"));
        }

        AppendTrustedGenerationViolations(result.Snapshot.PreviousAuthenticatedGeneration, result, violations);

        var anchorGateReport = Av3AnchorInvariantValidator.ValidatePhaseGates();
        if (!anchorGateReport.Passed)
        {
            violations.Add(Code(Av3WriterInvariant.NoProductionRouteWhileDisabled, "anchor_phase_gate"));
        }

        var cryptoGateReport = Crypto.Av3CryptoInvariantValidator.ValidatePhaseGates();
        if (!cryptoGateReport.Passed)
        {
            violations.Add(Code(Av3WriterInvariant.NoProductionRouteWhileDisabled, "crypto_phase_gate"));
        }

        var trustedGateReport = Av3TrustedAnchorInvariantValidator.ValidatePhaseGates();
        if (!trustedGateReport.Passed)
        {
            violations.Add(Code(Av3WriterInvariant.NoProductionRouteWhileDisabled, "trusted_anchor_phase_gate"));
        }

        var diskGateReport = Av3DiskDurabilityInvariantValidator.ValidatePhaseGates();
        if (!diskGateReport.Passed)
        {
            violations.Add(Code(Av3WriterInvariant.NoProductionRouteWhileDisabled, "disk_durability_phase_gate"));
        }

        return Report(violations);
    }

    public static Av3WriterInvariantReport ValidateAnchorPosture(
        bool pipelineCommitted,
        bool anchorUpdated,
        WriterDesign.Av3AnchorStatus anchorStatus)
    {
        var anchorReport = Av3AnchorInvariantValidator.ValidateAnchorPosture(
            pipelineCommitted,
            anchorUpdated,
            anchorStatus);
        if (anchorReport.Passed)
        {
            return Report([]);
        }

        return Report([Code(Av3WriterInvariant.NoProductionRouteWhileDisabled, "anchor_posture")]);
    }

    public static Av3WriterInvariantReport ValidateJournalBytes(ReadOnlySpan<byte> journalBytes)
    {
        var scan = Av3JournalConfidentialityScanner.Scan(journalBytes);
        if (scan.Passed)
        {
            return Report([]);
        }

        return Report([Code(Av3WriterInvariant.NoJournalCleartext, scan.PublicSummary)]);
    }

    public static Av3WriterInvariantReport ValidatePublicTextSurface(string? text, string channel)
    {
        var leak = Av3JournalLeakScanner.ScanText(text, channel);
        if (leak.Passed)
        {
            return Report([]);
        }

        return Report([Code(Av3WriterInvariant.NoSecretLog, $"leak_{channel}")]);
    }

    public static Av3WriterInvariantReport ValidateTrustedGenerationPreserved(
        ulong previousAuthenticated,
        Av3CommitPipelineRunner.Av3CommitPipelineResult result)
    {
        var violations = new List<Av3WriterInvariantViolation>();
        AppendTrustedGenerationViolations(previousAuthenticated, result, violations);
        return Report(violations);
    }

    private static void AppendTrustedGenerationViolations(
        ulong previousAuthenticated,
        Av3CommitPipelineRunner.Av3CommitPipelineResult result,
        List<Av3WriterInvariantViolation> violations)
    {
        var recovery = new Av3CommitRecoveryManager();
        var trustedOpen = recovery.AssessSnapshot(result.Snapshot, null).TrustedOpenGeneration;

        var commitComplete = result.Committed
            && result.PostAuthDataTrusted
            && result.Snapshot.ActivationAuthenticated
            && result.Snapshot.MetadataAuthenticated
            && result.Snapshot.CleanupCompleted
            && !result.Snapshot.CleanupFailed;

        if (commitComplete)
        {
            if (trustedOpen < previousAuthenticated)
            {
                violations.Add(Code(Av3WriterInvariant.OldGenerationPreservedUntilCommit, "trusted_below_previous"));
            }

            return;
        }

        if (trustedOpen > previousAuthenticated)
        {
            violations.Add(Code(Av3WriterInvariant.OldGenerationPreservedUntilCommit, "trusted_promoted_without_commit"));
        }

        if (!result.PostAuthDataTrusted
            && result.Snapshot.AttemptedTargetGeneration > previousAuthenticated
            && trustedOpen >= result.Snapshot.AttemptedTargetGeneration)
        {
            violations.Add(Code(Av3WriterInvariant.AuthBeforeTrust, "new_gen_trusted_pre_auth"));
        }

        if (result.Snapshot.CleanupFailed && trustedOpen > previousAuthenticated)
        {
            violations.Add(Code(Av3WriterInvariant.CleanupFailureSeparated, "cleanup_fail_trusted_promotion"));
        }
    }

    private static Av3WriterInvariantViolation Code(Av3WriterInvariant invariant, string code) =>
        new() { Invariant = invariant, PublicCode = code };

    private static Av3WriterInvariantReport Report(List<Av3WriterInvariantViolation> violations) =>
        new() { Passed = violations.Count == 0, Violations = violations };
}