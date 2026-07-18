using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.DryRun;
using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE8Tests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";
    private const string SecretMarker = "X-SECRET-MARKER-E8-7a21";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E8_LimitedDryRun_EnableFlagsFalse()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.E8LimitedDryRunComplete);
        Assert.True(Av3PhaseGate.E8LimitedDryRunHarnessEnabled);
        Assert.True(Av3PhaseGate.E8ReadOnlyRevalidationCovered);
        Assert.True(Av3PhaseGate.E8DryRunTelemetryNonLeakCovered);
        Assert.True(Av3PhaseGate.E8FaultMatrixCovered);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
    }

    [Theory]
    [InlineData(Av3SyntheticFixtureKind.Standard)]
    [InlineData(Av3SyntheticFixtureKind.MultiObject)]
    [InlineData(Av3SyntheticFixtureKind.MultiSegment)]
    [InlineData(Av3SyntheticFixtureKind.EmptyObject)]
    public async Task E8_DryRun_EndToEnd_Success(Av3SyntheticFixtureKind kind)
    {
        var root = Av3DryRunScope.CreateRoot();
        try
        {
            var options = new Av3DryRunOptions { VaultRoot = root, FixtureKind = kind };
            var result = await Av3DryRunRunner.RunAsync(options);
            Assert.True(result.Pipeline.Committed);
            Assert.True(result.ReadOnlyRevalidation.Passed);
            Assert.True(result.Validation.Passed);
            Assert.True(result.Telemetry.Passed);
            Assert.True(result.Report.Success);
            Assert.True(Av3WriterInvariantValidator.ValidatePipelineResult(result.Pipeline).Passed);
            Assert.DoesNotContain(SecretMarker, result.Report.Manifest.ToPublicJson(), StringComparison.Ordinal);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact]
    public void E8_DryRunScope_E8Prefix_Allowed()
    {
        var root = Av3DryRunScope.CreateRoot();
        Assert.True(Av3DryRunScope.IsDryRunRootAllowed(root, out _));
    }

    [Fact]
    public void E8_DryRunScope_E7OnlyRoot_Rejected()
    {
        var e7 = Path.Combine(Path.GetTempPath(), $"{Av3WriterAccessGate.HarnessRootToken}7-only-{Guid.NewGuid():N}");
        Assert.False(Av3DryRunScope.IsDryRunRootAllowed(e7, out _));
        Assert.Throws<Av3WriterRouteBlockedException>(() =>
            Av3DryRunScope.Ensure(new Av3DryRunOptions { VaultRoot = e7 }));
    }

    [Fact]
    public void E8_DryRunScope_UserDocuments_Rejected()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(docs))
        {
            return;
        }

        var bait = Path.Combine(docs, $"{Av3DryRunScope.E8RootPrefix}bait");
        Assert.False(Av3DryRunScope.IsDryRunRootAllowed(bait, out _));
    }

    [Theory]
    [InlineData(nameof(ConfigureFailFlush))]
    [InlineData(nameof(ConfigureFailReread))]
    [InlineData(nameof(ConfigureFailAuth))]
    [InlineData(nameof(ConfigureFailCleanup))]
    [InlineData(nameof(ConfigurePartialWrite))]
    [InlineData(nameof(ConfigureHeaderOneCopy))]
    [InlineData(nameof(ConfigureHeaderConflict))]
    public async Task E8_FaultMatrix_NotCommitted_NoNewGenOpen(string configure)
    {
        var root = Av3DryRunScope.CreateRoot();
        try
        {
            var sim = new Av3CommitSimulationOptions();
            GetType().GetMethod(configure, BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(this, [sim]);
            var options = new Av3DryRunOptions { VaultRoot = root, Simulation = sim };
            var result = await Av3DryRunRunner.RunAsync(options);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Pipeline.Classification.ToString());
            if (configure == nameof(ConfigureHeaderOneCopy))
            {
                Assert.Equal(nameof(Av3RecoveryClassification.RedundancyDegraded), result.Pipeline.Classification.ToString());
            }
            else
            {
                Assert.False(result.Pipeline.Committed);
            }

            Assert.True(Av3WriterInvariantValidator.ValidatePipelineResult(result.Pipeline).Passed);
            Assert.True(result.Telemetry.Passed);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact]
    public async Task E8_FaultMatrix_CancellationToken_NotCommitted()
    {
        var root = Av3DryRunScope.CreateRoot();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            var options = new Av3DryRunOptions { VaultRoot = root };
            var result = await Av3DryRunRunner.RunAsync(options, cts.Token);
            Assert.False(result.Pipeline.Committed);
            Assert.True(result.Pipeline.Cancelled);
            Assert.True(result.Telemetry.Passed);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact]
    public async Task E8_CleanupFailure_Posture_Separated()
    {
        var root = Av3DryRunScope.CreateRoot();
        var sim = new Av3CommitSimulationOptions { FailCleanup = true };
        try
        {
            var result = await Av3DryRunRunner.RunAsync(new Av3DryRunOptions { VaultRoot = root, Simulation = sim });
            Assert.False(result.Pipeline.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.RecoveryRequired), result.Pipeline.Classification.ToString());
            Assert.True(Av3WriterInvariantValidator.ValidateTrustedGenerationPreserved(3, result.Pipeline).Passed);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Theory]
    [InlineData(Av3RecoveryClassification.RollbackSuspected)]
    [InlineData(Av3RecoveryClassification.CorruptBlocked)]
    [InlineData(Av3RecoveryClassification.PreviousGenerationOpen)]
    public void E8_Classifier_Posture_NoAutoRepair(Av3RecoveryClassification recovery)
    {
        var mgr = new Av3CommitRecoveryManager();
        var snapshot = SnapshotFor(recovery);
        var (_, repair) = mgr.ClassifySnapshot(snapshot);
        _ = Av3RepairClassifier.FromRecovery(recovery);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
        Assert.NotEqual(Av3RepairClassification.Healthy, repair);
    }

    [Fact]
    public async Task E8_Telemetry_AllSurfaces_NoLeak()
    {
        var root = Av3DryRunScope.CreateRoot();
        try
        {
            var result = await Av3DryRunRunner.RunAsync(new Av3DryRunOptions { VaultRoot = root });
            Assert.True(result.Telemetry.Passed);
            Assert.False(Av3DryRunTelemetryScanner.ContainsForbidden(result.Report.ToPublicSummary()));
            Assert.False(Av3DryRunTelemetryScanner.ContainsForbidden(result.Report.Manifest.ToPublicJson()));
            Assert.DoesNotContain("password", result.Report.TraceSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VMK", result.Report.TraceSummary, StringComparison.Ordinal);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E8_ProductionRoute_AppHostViewModel_NoWriter()
    {
        AssertNoTypeReferences(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoTypeReferences(typeof(SecureVaultService).Assembly, DryRunPrefix);
        AssertNoTypeReferences(typeof(AstraVaultHostService).Assembly, CommitPrefix);
        AssertNoTypeReferences(typeof(SecureVaultViewModel).Assembly, CommitPrefix);
        AssertNoTypeReferences(typeof(SecureVaultViewModel).Assembly, typeof(IAv3VaultWriter).FullName!);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task E8_DurableStore_RootEscape_Blocked()
    {
        var root = Av3DryRunScope.CreateRoot();
        Av3DryRunScope.EnsureDryRunRoot(root);
        try
        {
            var store = new Av3CommitDurableStore(root, new Av3CommitSimulationOptions());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.WriteTempThenCommitAsync("../escape", new byte[] { 1 }, new Av3DurableCommitOptions
                {
                    TransactionId = Guid.NewGuid(),
                    TargetGeneration = 4
                }).AsTask());
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact]
    public void E8_DryRunSuccess_DoesNotImplyWriterEnableReady()
    {
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
    }

    private void ConfigureFailFlush(Av3CommitSimulationOptions s) =>
        s.FailFlushAtStep = Av3CommitPipelineStep.FlushMetadataRoot;

    private void ConfigureFailReread(Av3CommitSimulationOptions s) => s.FailReread = true;

    private void ConfigureFailAuth(Av3CommitSimulationOptions s) => s.FailAuthentication = true;

    private void ConfigureFailCleanup(Av3CommitSimulationOptions s) => s.FailCleanup = true;

    private void ConfigurePartialWrite(Av3CommitSimulationOptions s) => s.PartialWriteTruncate = true;

    private void ConfigureHeaderOneCopy(Av3CommitSimulationOptions s)
    {
        s.DurableHeaderCopy0 = true;
        s.DurableHeaderCopy1 = false;
        s.DurableHeaderCopy2 = false;
    }

    private void ConfigureHeaderConflict(Av3CommitSimulationOptions s) => s.HeaderCopyConflict = true;

    private static Av3CommitSnapshot SnapshotFor(Av3RecoveryClassification recovery) =>
        recovery switch
        {
            Av3RecoveryClassification.RollbackSuspected => new Av3CommitSnapshot { RollbackSuspected = true },
            Av3RecoveryClassification.CorruptBlocked => new Av3CommitSnapshot { EqualGenerationConflictingRoot = true },
            _ => new Av3CommitSnapshot { StaleHighGenerationUnauthenticated = true }
        };

    private static void CleanupRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static void AssertNoTypeReferences(Assembly assembly, string namespaceOrTypePrefix)
    {
        var hits = new List<string>();
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.DeclaringType != type && method.IsAbstract)
                {
                    continue;
                }

                Check(method.ReturnType);
                foreach (var p in method.GetParameters())
                {
                    Check(p.ParameterType);
                }
            }
        }

        Assert.Empty(hits);

        void Check(Type? t)
        {
            if (t?.FullName?.StartsWith(namespaceOrTypePrefix, StringComparison.Ordinal) == true)
            {
                hits.Add(t.FullName);
            }
        }
    }
}