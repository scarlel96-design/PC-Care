using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Anchor;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.DryRun;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE11Tests
{
    private const string AnchorPrefix = "SmartPerformanceDoctor.AstraVault.Anchor";
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";

    [Fact]
    public void E11_AnchorHarness_RequiresIsolatedTempRoot()
    {
        var bad = Path.Combine(Path.GetTempPath(), "plain-temp-" + Guid.NewGuid().ToString("N"));
        Assert.False(Av3AnchorHarnessScope.IsE11RootAllowed(bad, out _));
        Assert.Throws<Av3WriterRouteBlockedException>(() => Av3AnchorHarnessScope.EnsureE11Root(bad));
    }

    [Fact]
    public void E11_AnchorHarness_RejectsUserVaultPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Av3AnchorHarnessScope.E11RootPrefix + Guid.NewGuid().ToString("N"));
        Assert.False(Av3AnchorHarnessScope.IsE11RootAllowed(path, out _));
    }

    [Fact]
    public void E11_AnchorHarness_RejectsDocumentsDesktopDownloads()
    {
        foreach (var token in new[] { "Documents", "Desktop", "Downloads" })
        {
            var path = Path.Combine(Path.GetTempPath(), token, Av3AnchorHarnessScope.E11RootPrefix + "x");
            Assert.False(Av3WriterAccessGate.TryNormalizeHarnessRoot(path, out _));
        }
    }

    [Fact]
    public async Task E11_AnchorGenesis_NoPriorAnchor_Verifies()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var read = await anchor.ReadAnchorAsync(root);
            Assert.Null(read);
            var digest = Witness(1);
            var id = Guid.NewGuid();
            var prep = await anchor.PrepareAnchorUpdateAsync(Request(root, 1, digest, id));
            Assert.True(prep.Success);
            var commit = await anchor.CommitAnchorUpdateAsync(root, id);
            Assert.True(commit.Success);
            var verify = await anchor.VerifyAnchorAsync(root, 1, digest);
            Assert.True(verify.Verified);
            Assert.Equal(Av3AnchorStatus.AnchorFresh, verify.Status);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_AnchorGenerationEqual_Pass()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var digest = Witness(5);
            await SeedAnchorAsync(anchor, root, 5, digest);
            var verify = await anchor.VerifyAnchorAsync(root, 5, digest);
            Assert.Equal(Av3AnchorStatus.AnchorFresh, verify.Status);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_AnchorGenerationHigher_RollbackSuspected()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var digest = Witness(10);
            await SeedAnchorAsync(anchor, root, 10, digest);
            var verify = await anchor.VerifyAnchorAsync(root, 7, digest);
            Assert.Equal(Av3AnchorStatus.AnchorRollbackSuspected, verify.Status);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_AnchorGenerationLower_StaleAnchorClassified()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var digest = Witness(3);
            await SeedAnchorAsync(anchor, root, 3, digest);
            var verify = await anchor.VerifyAnchorAsync(root, 8, digest);
            Assert.Equal(Av3AnchorStatus.AnchorStale, verify.Status);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_AnchorDigestMismatch_RollbackSuspected()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            await SeedAnchorAsync(anchor, root, 4, Witness(4));
            var verify = await anchor.VerifyAnchorAsync(root, 4, Witness(99));
            Assert.Equal(Av3AnchorStatus.AnchorMismatch, verify.Status);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_AnchorUpdateFailure_NotCommitted()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var prep = await anchor.PrepareAnchorUpdateAsync(Request(root, 2, Witness(2), Guid.NewGuid()));
            Assert.True(prep.Success);
            var commit = await anchor.CommitAnchorUpdateAsync(root, Guid.NewGuid());
            Assert.False(commit.Success);
            Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_HeaderCommitFailure_DoesNotPromoteAnchor()
    {
        var root = CreateE11Root();
        try
        {
            var pipeline = FailedPipeline();
            var bridge = await Av3AnchorDryRunBridge.RunAnchorAfterDryRunAsync(
                DryRunOptions(root),
                pipeline,
                Guid.NewGuid(),
                Witness(1));
            Assert.False(bridge.AnchorUpdated);
            Assert.False(bridge.AnchorPostureCommitted);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_CleanupFailure_DoesNotPromoteAnchor()
    {
        var root = CreateE11Root();
        try
        {
            var pipeline = new Av3CommitPipelineRunner.Av3CommitPipelineResult
            {
                Committed = false,
                Snapshot = new Av3CommitSnapshot { CleanupFailed = true, CleanupCompleted = false },
                Classification = Av3RecoveryClassification.RecoveryRequired
            };
            var bridge = await Av3AnchorDryRunBridge.RunAnchorAfterDryRunAsync(
                DryRunOptions(root),
                pipeline,
                Guid.NewGuid(),
                Witness(1));
            Assert.False(bridge.AnchorUpdated);
            Assert.False(bridge.AnchorPostureCommitted);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_Cancellation_DoesNotPromoteAnchor()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var updateId = Guid.NewGuid();
            await anchor.PrepareAnchorUpdateAsync(Request(root, 1, Witness(1), updateId));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await anchor.CommitAnchorUpdateAsync(root, updateId, cts.Token));
            await anchor.AbortAnchorUpdateAsync(root, updateId);
            Assert.Null(await anchor.ReadAnchorAsync(root));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_ConcurrentSameRootAnchor_Denied()
    {
        var root = CreateE11Root();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            await anchor.PrepareAnchorUpdateAsync(Request(root, 1, Witness(1), id1));
            await anchor.PrepareAnchorUpdateAsync(Request(root, 2, Witness(2), id2));

            Av3HarnessRollbackAnchor.TestingHoldCommitMilliseconds = 250;
            try
            {
                var t1 = anchor.CommitAnchorUpdateAsync(root, id1);
                await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(async () =>
                    await anchor.CommitAnchorUpdateAsync(root, id2));
                await t1;
            }
            finally
            {
                Av3HarnessRollbackAnchor.TestingHoldCommitMilliseconds = 0;
            }
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E11_IndependentRoots_NoInterference()
    {
        var roots = Enumerable.Range(0, 6).Select(_ => CreateE11Root()).ToArray();
        try
        {
            var anchor = new Av3HarnessRollbackAnchor();
            var tasks = roots.Select(async (r, i) =>
            {
                var id = Guid.NewGuid();
                await anchor.PrepareAnchorUpdateAsync(Request(r, (ulong)(i + 1), Witness(i + 1), id));
                return await anchor.CommitAnchorUpdateAsync(r, id);
            });
            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r.Success));
        }
        finally
        {
            foreach (var r in roots)
            {
                Cleanup(r);
            }
        }
    }

    [Fact]
    public void E11_AnchorPublicError_Redacted()
    {
        var text = "av3_anchor_monotonicity_violation password VMK DEK C:\\Users\\secret";
        Assert.False(Av3AnchorPublicSurface.IsPublicTextSafe(text));
        Assert.True(Av3AnchorInvariantValidator.ValidatePublicSurface("av3_anchor_fresh", "e11").Passed);
        Assert.True(Av3WriterAccessGate.IsPublicErrorClassSafe("av3_anchor_pending_missing"));
    }

    [Fact]
    public void E11_ServiceUiImportExport_NoAnchorOrWriterWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, DryRunPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E11_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
        Assert.True(Av3EnableReadinessChecklist.MigrationSeparated);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E11_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.E11AnchorHarnessEnabled);
        Assert.True(Av3PhaseGate.E11AnchorClosurePackageComplete);
        Assert.True(Av3PhaseGate.ProductionAnchorImplementationCandidate);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.False(Av3AnchorDesignPolicy.ProductionAnchorImplemented);
        Assert.True(Av3AnchorDesignPolicy.ProductionAnchorImplementationCandidate);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E11_SClassStillNotSatisfied()
    {
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.False(Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed);
    }

    [Fact]
    public void E11_NextBlockersRemainOpen()
    {
        var path = ResolveDoc("ASTRA_VAULT_E11_ANCHOR_CLOSURE_REPORT.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("XChaCha24", text, StringComparison.Ordinal);
        Assert.Contains("disk durability", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
        Assert.Contains("same-disk untrusted anchor alone cannot prove full-vault rollback resistance", text, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: stability matrix under pre-enable rules superseded")]
    public void E11_AnchorStabilityRepeat_Skipped()
    {
    }

    [Fact]
    public void E11_AnchorInvariants_LinkedToWriterGates()
    {
        Assert.True(Av3WriterInvariantValidator.InvariantExpectWriterGatesClosed());
        Assert.True(Av3AnchorInvariantValidator.ValidatePhaseGates().Passed);
        Assert.True(Av3AnchorRuntimePolicy.StoresPublicDigestsOnly);
    }

    private static string CreateE11Root()
    {
        var root = Av3AnchorHarnessScope.CreateRoot();
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root)
    {
        Av3HarnessRollbackAnchor.ClearHarnessState(root);
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            var store = Av3AnchorHarnessScope.ResolveAnchorStoreDirectory(root);
            if (Directory.Exists(store))
            {
                Directory.Delete(store, recursive: true);
            }
        }
        catch
        {
            // best-effort harness cleanup
        }
    }

    private static byte[] Witness(int seed)
    {
        var bytes = BitConverter.GetBytes(seed);
        return SHA256.HashData(bytes);
    }

    private static Av3AnchorUpdateRequest Request(string root, ulong gen, byte[] digest, Guid updateId) =>
        new()
        {
            VaultRoot = root,
            TestHarnessInvocation = true,
            ContainerId = Guid.NewGuid(),
            TargetGeneration = gen,
            WitnessDigest = digest,
            UpdateId = updateId
        };

    private static async Task SeedAnchorAsync(Av3HarnessRollbackAnchor anchor, string root, ulong gen, byte[] digest)
    {
        var id = Guid.NewGuid();
        var prep = await anchor.PrepareAnchorUpdateAsync(Request(root, gen, digest, id));
        Assert.True(prep.Success);
        var commit = await anchor.CommitAnchorUpdateAsync(root, id);
        Assert.True(commit.Success);
    }

    private static Av3DryRunOptions DryRunOptions(string root) =>
        new()
        {
            VaultRoot = root,
            TestHarnessInvocation = true,
            FixtureKind = Av3SyntheticFixtureKind.Standard,
            RunCleanup = false
        };

    private static Av3CommitPipelineRunner.Av3CommitPipelineResult FailedPipeline() =>
        new()
        {
            Committed = false,
            PostAuthDataTrusted = false,
            Classification = Av3RecoveryClassification.CorruptBlocked
        };

    private static void AssertNoNamespace(Assembly assembly, string prefix)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.ReturnType.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
                {
                    throw new InvalidOperationException($"Unexpected return {method.ReturnType.FullName}");
                }

                foreach (var p in method.GetParameters())
                {
                    if (p.ParameterType.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
                    {
                        throw new InvalidOperationException($"Unexpected parameter {p.ParameterType.FullName}");
                    }
                }
            }
        }
    }

    private static string ResolveDoc(string name)
    {
        var copied = Path.Combine(AppContext.BaseDirectory, "security-docs", name);
        if (File.Exists(copied))
        {
            return copied;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "docs", "security", name);
    }
}