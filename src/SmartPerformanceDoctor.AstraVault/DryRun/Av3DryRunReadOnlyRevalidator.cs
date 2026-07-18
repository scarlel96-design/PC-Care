using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;
using SmartPerformanceDoctor.AstraVault.Journal;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

public sealed class Av3DryRunReadOnlyRevalidation
{
    public bool Passed { get; init; }

    public string PublicSummary { get; init; } = "ro_reval_skipped";
}

/// <summary>Re-runs read-only trust chain on dry-run artifacts (harness only).</summary>
public static class Av3DryRunReadOnlyRevalidator
{
    public static Av3DryRunReadOnlyRevalidation Revalidate(
        string dryRunRoot,
        ReadOnlySpan<byte> vmk,
        Av3CommitPipelineRunner.Av3CommitPipelineResult pipeline)
    {
        if (!pipeline.Committed || !pipeline.PostAuthDataTrusted)
        {
            return new Av3DryRunReadOnlyRevalidation
            {
                Passed = true,
                PublicSummary = "ro_reval_not_required_uncommitted"
            };
        }

        var store = new Av3CommitDurableStore(dryRunRoot, new Av3CommitSimulationOptions());
        var header = store.RereadRelative(Av3DurableFileLayout.ActivationRelative);
        var meta = store.RereadRelative(Av3DurableFileLayout.MetadataRelative);
        var journal = store.RereadRelative(Av3DurableFileLayout.JournalRelative);
        if (header is null || meta is null || journal is null)
        {
            return Fail("missing_artifact");
        }

        var auth = Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(header, meta, vmk);
        if (!auth.Success)
        {
            return Fail("auth_chain_failed");
        }

        var journalScan = Av3JournalConfidentialityScanner.Scan(journal);
        if (!journalScan.Passed)
        {
            return Fail("journal_policy_failed");
        }

        var copy0 = store.RereadRelative(Av3DurableFileLayout.HeaderCopy0);
        var copy1 = store.RereadRelative(Av3DurableFileLayout.HeaderCopy1);
        var copy2 = store.RereadRelative(Av3DurableFileLayout.HeaderCopy2);
        if (copy0 is null)
        {
            return Fail("header_copy_missing");
        }

        var state = new Av3HeaderCopyDurabilityState
        {
            Copy0Durable = copy0 is not null,
            Copy1Durable = copy1 is not null,
            Copy2Durable = copy2 is not null,
            Copy1ConflictsWithCopy2 = pipeline.Snapshot.HeaderCopyConflict
        };
        _ = Av3HeaderCopyRecoveryClassifier.Classify(state, activationAuthenticated: true);

        return new Av3DryRunReadOnlyRevalidation
        {
            Passed = true,
            PublicSummary = "ro_reval_ok"
        };
    }

    private static Av3DryRunReadOnlyRevalidation Fail(string code) =>
        new() { Passed = false, PublicSummary = $"ro_reval_fail_{code}" };
}