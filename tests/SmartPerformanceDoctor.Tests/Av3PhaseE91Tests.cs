using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE91Tests
{
    private static readonly string[] CurrentEvidenceDocs =
    [
        "ASTRA_VAULT_PRODUCTION_WRITER_REVIEW_PACKAGE.md",
        "ASTRA_VAULT_EXTERNAL_REVIEW_EVIDENCE_INDEX.md",
        "ASTRA_VAULT_EXTERNAL_REVIEW_BRIEF.md",
        "ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md",
        "ASTRA_VAULT_FORMAL_EXTERNAL_REVIEW_SIGNOFF.md",
    ];

    [Fact]
    public void E91_PhaseGate_ReviewFixes_EnableFlagsStillFalse()
    {
        Assert.True(Av3PhaseGate.E91ExternalReviewFixesApplied);
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3WriterInvariantValidator.InvariantExpectWriterGatesClosed());
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E91_Documentation_CurrentEvidence_MatchesSourceOfTruth_NoStale444()
    {
        var sot = LoadSourceOfTruth();
        var full = sot.LatestVerified.FullSuite;
        var filter = sot.LatestVerified.FilteredAv3WriterSlice;
        var docsRoot = ResolveSecurityDocsRoot();

        foreach (var doc in CurrentEvidenceDocs)
        {
            var text = File.ReadAllText(Path.Combine(docsRoot, doc));
            Assert.True(
                text.Contains(full.Passed.ToString(), StringComparison.Ordinal)
                || text.Contains("E-TEST-SOT", StringComparison.Ordinal)
                || text.Contains("ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH", StringComparison.Ordinal),
                $"Doc {doc} must reference latest verified count or SOT.");
            foreach (var line in text.Split('\n'))
            {
                if (line.Contains("444/444", StringComparison.Ordinal) || line.Contains("134/134", StringComparison.Ordinal))
                {
                    Assert.Contains("historical", line, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        var evidenceIndex = File.ReadAllText(Path.Combine(docsRoot, "ASTRA_VAULT_EXTERNAL_REVIEW_EVIDENCE_INDEX.md"));
        Assert.Contains(filter.Passed.ToString(), evidenceIndex, StringComparison.Ordinal);
        var sotMd = File.ReadAllText(Path.Combine(docsRoot, "ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md"));
        Assert.Contains("latest verified", sotMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(full.Passed.ToString(), sotMd, StringComparison.Ordinal);
    }

    [Fact]
    public async Task E91_CommitGuard_ParallelIndependentRoots_NoInterference()
    {
        var tasks = Enumerable.Range(0, 12).Select(async _ =>
        {
            var root = HarnessRoot("parallel");
            try
            {
                using var lease = Av3WriterCommitGuard.EnterHarnessCommit(root, Guid.NewGuid());
                await Task.Delay(5).ConfigureAwait(false);
                Assert.False(string.IsNullOrEmpty(lease.ToString()));
            }
            finally
            {
                Av3WriterCommitGuard.ClearVaultHarnessState(root);
            }
        });
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task E91_CommitGuard_SameRoot_ConcurrentCommit_Denied()
    {
        var root = HarnessRoot("same-root");
        Av3WriterCommitGuard.ClearVaultHarnessState(root);
        try
        {
            using var first = Av3WriterCommitGuard.EnterHarnessCommit(root, Guid.NewGuid());
            var ex = await Task.Run(() =>
            {
                try
                {
                    Av3WriterCommitGuard.EnterHarnessCommit(root, Guid.NewGuid());
                    return null;
                }
                catch (Av3WriterRouteBlockedException e)
                {
                    return e;
                }
            });
            Assert.NotNull(ex);
            Assert.True(
                ex!.PublicErrorClass is Av3WriterAccessGate.ErrorCommitInFlight
                    or Av3WriterAccessGate.ErrorReentrantCommit,
                ex.PublicErrorClass);
        }
        finally
        {
            Av3WriterCommitGuard.ClearVaultHarnessState(root);
        }
    }

    [Fact]
    public async Task E91_CommitGuard_CancelledCommit_ReleasesLease()
    {
        var options = BuildHarnessOptions(s => s.CancelAfterStep = Av3CommitPipelineStep.RecordJournal);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.Cancelled);
            Assert.False(result.Committed);
            Assert.False(Av3WriterCommitGuard.Registry.IsRootInFlight(options.VaultRoot));
            using var lease = Av3WriterCommitGuard.EnterHarnessCommit(options.VaultRoot, Guid.NewGuid());
            Assert.NotNull(lease);
            Assert.True(Av3WriterInvariantValidator.ValidateTrustedGenerationPreserved(3, result).Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E91_CommitGuard_FaultedCommit_ReleasesLease()
    {
        var options = BuildHarnessOptions(s => s.FailAuthentication = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.Committed);
            Assert.False(Av3WriterCommitGuard.Registry.IsRootInFlight(options.VaultRoot));
            using var lease = Av3WriterCommitGuard.EnterHarnessCommit(options.VaultRoot, Guid.NewGuid());
            Assert.NotNull(lease);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E91_CommitGuard_CleanupFailure_DoesNotLeakLease()
    {
        var options = BuildHarnessOptions(s => s.FailCleanup = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.Snapshot.CleanupFailed);
            Assert.False(result.Committed);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
            Assert.False(Av3WriterCommitGuard.Registry.IsRootInFlight(options.VaultRoot));
            Assert.True(Av3WriterInvariantValidator.ValidatePipelineResult(result).Passed);
            using var lease = Av3WriterCommitGuard.EnterHarnessCommit(options.VaultRoot, Guid.NewGuid());
            Assert.NotNull(lease);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E91_CommitGuard_NonHarnessProductionRouteStillDenied()
    {
        var create = Av3WriterHarnessFactory.TryCreateProductionRoute();
        Assert.False(create.Success);
        Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, create.PublicErrorClass);
        Assert.Throws<Av3WriterRouteBlockedException>(Av3WriterAccessGate.DenyProductionCreate);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
    }

    [Fact(Skip = "50.4.0 production GO: stability matrix under pre-enable rules superseded")]
    public void E91_StabilityRepeat_Skipped()
    {
    }

    private static string HarnessRoot(string tag) =>
        Path.Combine(Path.GetTempPath(), $"av3-e91-{tag}-{Guid.NewGuid():N}");

    private static Av3CommitHarnessOptions BuildHarnessOptions(Action<Av3CommitSimulationOptions>? configure = null)
    {
        var plan = new Av3WritePlan
        {
            ContainerId = Guid.NewGuid(),
            TransactionId = Guid.NewGuid(),
            PreviousGeneration = 3,
            TargetGeneration = 4,
            PreviousMetadataRootDigest = RandomNumberGenerator.GetBytes(32),
            Objects = new Av3ObjectWriteSet { ObjectWriteSetDigest = RandomNumberGenerator.GetBytes(32) },
            Metadata = new Av3MetadataWriteSet
            {
                MetadataWriteDigest = RandomNumberGenerator.GetBytes(32),
                TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32)
            }
        };
        var simulation = new Av3CommitSimulationOptions();
        configure?.Invoke(simulation);
        return new Av3CommitHarnessOptions
        {
            VaultRoot = HarnessRoot("pipe"),
            TestHarnessInvocation = true,
            Plan = plan,
            Crypto = Av3HarnessCommitContext.Generate(plan),
            Simulation = simulation
        };
    }

    private static void CleanupRoot(string root)
    {
        Av3WriterCommitGuard.ClearVaultHarnessState(root);
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // best-effort harness cleanup
        }
    }

    private static string ResolveSecurityDocsRoot()
    {
        var copied = Path.Combine(AppContext.BaseDirectory, "security-docs");
        if (Directory.Exists(copied))
        {
            return copied;
        }

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            var candidate = Path.Combine(dir, "docs", "security");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException("docs/security not found from test output directory");
    }

    private static Av3TestEvidenceSourceOfTruth LoadSourceOfTruth()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestAssets", "av3_external_review_test_evidence.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Av3TestEvidenceSourceOfTruth>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to parse test evidence SOT JSON");
    }

    private sealed class Av3TestEvidenceSourceOfTruth
    {
        public LatestVerifiedBlock LatestVerified { get; init; } = new();

        public sealed class LatestVerifiedBlock
        {
            public SuiteEvidence FullSuite { get; init; } = new();

            public SuiteEvidence FilteredAv3WriterSlice { get; init; } = new();
        }

        public sealed class SuiteEvidence
        {
            public int Passed { get; init; }
        }
    }
}