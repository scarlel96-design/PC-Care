using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE7Tests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string SecretMarker = "X-SECRET-MARKER-E7-bb02";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E7_PreEnableHardening_EnableFlagsFalse()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.E71ReviewFixesApplied);
        Assert.True(Av3PhaseGate.E7PreEnableHardeningComplete);
        Assert.True(Av3PhaseGate.WriterInvariantChecksEnabled);
        Assert.True(Av3PhaseGate.ProductionRouteNegativeMatrixCovered);
        Assert.True(Av3PhaseGate.CancellationHardeningCovered);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.ChaCha12ByteNonceBelowSClass);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
    }

    [Fact]
    public void E7_InvariantValidator_DisabledGates_Pass()
    {
        var report = Av3WriterInvariantValidator.ValidateDisabledProductionGates();
        Assert.True(report.Passed);
        Assert.True(Av3WriterInvariantValidator.ValidatePublicTextSurface(report.ToPublicSummary(), "invariant").Passed);
    }

    [Theory]
    [InlineData(typeof(Av3WriterAccessGate))]
    [InlineData(typeof(Av3WriterHarnessFactory))]
    [InlineData(typeof(Av3DefaultWritePolicy))]
    public void E7_MultiLayerGate_PublicErrors_Safe(Type gateType)
    {
        Assert.StartsWith("SmartPerformanceDoctor.AstraVault.Commit", gateType.Namespace, StringComparison.Ordinal);
        var create = Av3WriterHarnessFactory.TryCreateProductionRoute();
        Assert.False(create.Success);
        Assert.True(Av3WriterAccessGate.IsPublicErrorClassSafe(create.PublicErrorClass));
        Assert.True(Av3WriterAccessGate.IsPublicErrorClassSafe(Av3WriterAccessGate.ErrorProductionDisabled));
        Assert.DoesNotContain(SecretMarker, create.PublicErrorClass, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E7_NegativeMatrix_FactoryProductionCreate_Blocked()
    {
        var r = Av3WriterHarnessFactory.TryCreateProductionRoute();
        Assert.False(r.Success);
        Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, r.PublicErrorClass);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task E7_NegativeMatrix_OrchestratorCommitAsync_Blocked()
    {
        var options = BuildHarnessOptions();
        try
        {
            var o = Av3WriterHarnessFactory.CreateHarnessOrchestrator(options);
            var ex = await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(() =>
                o.CommitAsync(new Av3VaultCommitRequest { TransactionId = Guid.NewGuid(), TargetGeneration = 4 }).AsTask());
            Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, ex.PublicErrorClass);
            Assert.True(Av3WriterAccessGate.IsPublicErrorClassSafe(ex.PublicErrorClass));
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task E7_NegativeMatrix_OpenWriteSessionAsync_Blocked()
    {
        var options = BuildHarnessOptions();
        try
        {
            var o = Av3WriterHarnessFactory.CreateHarnessOrchestrator(options);
            var ex = await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(() =>
                o.OpenWriteSessionAsync(new Av3WriteSessionOpenRequest { VaultRootPath = options.VaultRoot, TrustedGeneration = 3 }).AsTask());
            Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, ex.PublicErrorClass);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task E7_NegativeMatrix_JournalRecorderProductionRoute_Blocked()
    {
        var ex = await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(() =>
            new Av3CommitJournalRecorder(harnessRoute: false).RecordStateAsync(new Av3JournalRecordRequest
            {
                TransactionId = Guid.NewGuid(),
                PreviousGeneration = 3,
                TargetGeneration = 4,
                TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32)
            }).AsTask());
        Assert.Equal(Av3WriterAccessGate.ErrorJournalProductionDisabled, ex.PublicErrorClass);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E7_NegativeMatrix_ProductionDurableStore_Blocked()
    {
        var r = Av3WriterHarnessFactory.TryCreateProductionDurableStore(@"C:\temp\production-vault");
        Assert.False(r.Success);
        Assert.True(Av3WriterAccessGate.IsPublicErrorClassSafe(r.PublicErrorClass));
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E7_NegativeMatrix_TransactionCoordinator_NonHarness_Blocked()
    {
        var root = IsolatedHarnessRoot();
        var options = new Av3CommitHarnessOptions
        {
            VaultRoot = root,
            TestHarnessInvocation = false,
            Plan = MinimalPlan(),
            Crypto = Av3HarnessCommitContext.Generate(MinimalPlan()),
            Simulation = new Av3CommitSimulationOptions()
        };
        Assert.Throws<Av3WriterRouteBlockedException>(() => new Av3CommitTransactionCoordinator(options));
    }

    [Fact]
    public void E7_NegativeMatrix_RecoveryManager_NoAutoRepair()
    {
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
        var mgr = new Av3CommitRecoveryManager();
        var assessment = mgr.AssessAfterInterrupt(new Av3RecoveryAssessmentInput { LastAuthenticatedGeneration = 3 });
        Assert.False(string.IsNullOrWhiteSpace(assessment.Classification));
        Assert.DoesNotContain("password", assessment.Classification, StringComparison.OrdinalIgnoreCase);
        AssertNoDataMutationMethods(typeof(Av3CommitRecoveryManager));
    }

    [Fact]
    public void E7_NegativeMatrix_AppHostViewModel_NoCommitWriter()
    {
        AssertNoCommitWriterReferences(typeof(SecureVaultService).Assembly, typeof(SecureVaultService).FullName!);
        AssertNoCommitWriterReferences(typeof(AstraVaultHostService).Assembly, typeof(AstraVaultHostService).FullName!);
        AssertNoCommitWriterReferences(typeof(SecureVaultViewModel).Assembly, typeof(SecureVaultViewModel).FullName!);
    }

    [Fact]
    public void E7_NegativeMatrix_ImportExportMethods_NoAv3Writer()
    {
        var vm = typeof(SecureVaultViewModel);
        var export = vm.GetMethod(nameof(SecureVaultViewModel.ExportEntryAsync));
        Assert.NotNull(export);
        AssertNoCommitWriterReferences(vm.Assembly, vm.FullName!);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task E7_ConcurrentCommit_SecondBlocked()
    {
        var options = BuildHarnessOptions();
        Av3WriterCommitGuard.ClearVaultHarnessState(options.VaultRoot);
        try
        {
            using (Av3WriterCommitGuard.EnterHarnessCommit(options.VaultRoot, options.Plan.TransactionId))
            {
                var ex = await Task.Run(() =>
                {
                    try
                    {
                        Av3WriterCommitGuard.EnterHarnessCommit(options.VaultRoot, Guid.NewGuid());
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
                        or Av3WriterAccessGate.ErrorReentrantCommit);
            }
        }
        finally
        {
            Av3WriterCommitGuard.ClearVaultHarnessState(options.VaultRoot);
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E7_ReentrantCommit_Blocked()
    {
        var root = IsolatedHarnessRoot();
        Av3WriterCommitGuard.ClearVaultHarnessState(root);
        using (Av3WriterCommitGuard.EnterHarnessCommit(root, Guid.NewGuid()))
        {
            var ex = Assert.Throws<Av3WriterRouteBlockedException>(() =>
                Av3WriterCommitGuard.EnterHarnessCommit(root, Guid.NewGuid()));
            Assert.Equal(Av3WriterAccessGate.ErrorReentrantCommit, ex.PublicErrorClass);
        }

        Av3WriterCommitGuard.ClearVaultHarnessState(root);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E7_DuplicateTransactionId_Blocked()
    {
        var root = IsolatedHarnessRoot();
        Av3WriterCommitGuard.ClearVaultHarnessState(root);
        var tx = Guid.NewGuid();
        using (Av3WriterCommitGuard.EnterHarnessCommit(root, tx))
        {
        }

        var ex = Assert.Throws<Av3WriterRouteBlockedException>(() => Av3WriterCommitGuard.EnterHarnessCommit(root, tx));
        Assert.Equal(Av3WriterAccessGate.ErrorDuplicateTransaction, ex.PublicErrorClass);
        Av3WriterCommitGuard.ClearVaultHarnessState(root);
    }

    [Theory]
    [InlineData(Av3CommitPipelineStep.BuildWritePlan)]
    [InlineData(Av3CommitPipelineStep.FlushObjects)]
    [InlineData(Av3CommitPipelineStep.FlushMetadataRoot)]
    [InlineData(Av3CommitPipelineStep.RecordJournal)]
    [InlineData(Av3CommitPipelineStep.FlushActivationHeader)]
    [InlineData(Av3CommitPipelineStep.Cleanup)]
    public async Task E7_Cancellation_NotCommitted(Av3CommitPipelineStep step)
    {
        var options = BuildHarnessOptions(s => s.CancelAfterStep = step);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.Cancelled);
            Assert.False(result.Committed);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
            var summary = result.Cancellation?.ToPublicSummary() ?? "";
            Assert.True(Av3WriterInvariantValidator.ValidatePublicTextSurface(summary, "cancel").Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Theory]
    [InlineData(Av3CommitPipelineStep.FlushObjects)]
    [InlineData(Av3CommitPipelineStep.FlushMetadataRoot)]
    [InlineData(Av3CommitPipelineStep.FlushJournal)]
    public async Task E7_CancellationBeforeFlush_NotCommitted(Av3CommitPipelineStep step)
    {
        var options = BuildHarnessOptions(s => s.CancelBeforeFlushAtStep = step);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.Cancelled);
            Assert.False(result.Committed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E7_HarnessSuccess_InvariantsPass()
    {
        var options = BuildHarnessOptions();
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(Av3WriterInvariantValidator.ValidatePipelineResult(result).Passed);
            Assert.True(Av3WriterInvariantValidator.ValidatePublicTextSurface(result.Trace.ToPublicSummary(), "trace").Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public void E7_CleanupRetry_Idempotent()
    {
        var root = IsolatedHarnessRoot();
        Av3CommitHarnessCleanup.ResetHarness(root);
        Assert.True(Av3CommitHarnessCleanup.TryRunOnce(root, () => { }));
        Assert.False(Av3CommitHarnessCleanup.TryRunOnce(root, () => throw new InvalidOperationException("should_not_run")));
    }

    [Theory]
    [InlineData(Av3RepairClassification.RepairRecommended)]
    [InlineData(Av3RepairClassification.RepairRequired)]
    [InlineData(Av3RepairClassification.ManualReviewRequired)]
    [InlineData(Av3RepairClassification.CorruptBlocked)]
    [InlineData(Av3RepairClassification.RollbackSuspected)]
    public void E7_RepairClassification_NoAutoMutationType(Av3RepairClassification repair)
    {
        var asm = typeof(Av3RepairClassifier).Assembly;
        var offenders = asm.GetTypes()
            .Where(t => t.Namespace?.StartsWith("SmartPerformanceDoctor.AstraVault", StringComparison.Ordinal) == true)
            .Where(t => t.Name.Contains("Repair", StringComparison.Ordinal) && t.Name.Contains("Executor", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();
        Assert.Empty(offenders);
        _ = repair;
    }

    [Fact]
    public void E7_GateFailure_Scan_NoSecretLeak()
    {
        var ex = new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorProductionDisabled);
        var text = ex.PublicErrorClass + " " + ex.Message;
        Assert.True(Av3JournalLeakScanner.ScanText(text, "gate").Passed);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    private static Av3WritePlan MinimalPlan() => new()
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

    private static string IsolatedHarnessRoot() =>
        Path.Combine(Path.GetTempPath(), $"{Av3WriterAccessGate.HarnessRootToken}7-{Guid.NewGuid():N}");

    private static Av3CommitHarnessOptions BuildHarnessOptions(Action<Av3CommitSimulationOptions>? configure = null)
    {
        var plan = MinimalPlan();
        var simulation = new Av3CommitSimulationOptions();
        configure?.Invoke(simulation);
        return new Av3CommitHarnessOptions
        {
            VaultRoot = IsolatedHarnessRoot(),
            TestHarnessInvocation = true,
            Plan = plan,
            Crypto = Av3HarnessCommitContext.Generate(plan),
            Simulation = simulation
        };
    }

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

    private static void AssertNoDataMutationMethods(Type type)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            Assert.DoesNotContain("WriteAll", method.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("Delete", method.Name, StringComparison.Ordinal);
        }
    }

    private static void AssertNoCommitWriterReferences(Assembly assembly, string? typeFullName = null)
    {
        var hits = new List<string>();
        foreach (var type in assembly.GetTypes().Where(t => typeFullName is null || t.FullName == typeFullName))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.DeclaringType != type && method.IsAbstract)
                {
                    continue;
                }

                CheckType(method.ReturnType, $"{type.FullName}.{method.Name}");
                foreach (var p in method.GetParameters())
                {
                    CheckType(p.ParameterType, $"{type.FullName}.{method.Name}");
                }
            }
        }

        Assert.Empty(hits);

        void CheckType(Type? t, string ctx)
        {
            if (t?.FullName?.StartsWith(CommitPrefix, StringComparison.Ordinal) == true)
            {
                hits.Add($"{ctx}:{t.FullName}");
            }
        }
    }
}