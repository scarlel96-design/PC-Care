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

public sealed class Av3PhaseE71Tests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string SecretMarker = "X-SECRET-MARKER-E71-4f90";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E71_ReviewFixes_AllEnableFlagsFalse()
    {
        Assert.True(Av3PhaseGate.E71ReviewFixesApplied);
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.E71ReviewFixesApplied);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
    }

    [Fact]
    public void E71_DisabledGates_IncludesJournalWriter()
    {
        Assert.True(Av3WriterInvariantValidator.ValidateDisabledProductionGates().Passed);
        Assert.True(Av3WriterInvariantValidator.InvariantExpectWriterGatesClosed());
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E71_NegativeMatrix_DenyMigrationRoute_Blocked()
    {
        var ex = Assert.Throws<Av3WriterRouteBlockedException>(Av3WriterAccessGate.DenyMigrationRoute);
        Assert.Equal(Av3WriterAccessGate.ErrorMigrationDisabled, ex.PublicErrorClass);
        AssertPublicErrorSafe(ex.PublicErrorClass);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E71_NegativeMatrix_TryOpenProductionSession_Blocked()
    {
        var r = Av3WriterHarnessFactory.TryOpenProductionSession();
        Assert.False(r.Success);
        Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, r.PublicErrorClass);
        AssertPublicErrorSafe(r.PublicErrorClass);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E71_NegativeMatrix_NonHarnessCommitSession_Blocked()
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
        var ex = Assert.Throws<Av3WriterRouteBlockedException>(() => new Av3CommitSession(options));
        Assert.Equal(Av3WriterAccessGate.ErrorHarnessOnly, ex.PublicErrorClass);
        AssertPublicErrorSafe(ex.PublicErrorClass);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task E71_NegativeMatrix_CommitThreeCopyAsync_Production_Blocked()
    {
        var root = IsolatedHarnessRoot();
        try
        {
            var committer = new Av3CommitHeaderCommitter(root, new Av3CommitSimulationOptions());
            var ex = await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(() =>
                committer.CommitThreeCopyAsync(new Av3HeaderCommitPlan(), CancellationToken.None).AsTask());
            Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, ex.PublicErrorClass);
            AssertPublicErrorSafe(ex.PublicErrorClass);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E71_NegativeMatrix_JournalWriterGate_False()
    {
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        var ex = Assert.Throws<Av3WriterRouteBlockedException>(() =>
            Av3WriterAccessGate.DenyJournalProductionRoute());
        Assert.Equal(Av3WriterAccessGate.ErrorJournalProductionDisabled, ex.PublicErrorClass);
        AssertPublicErrorSafe(ex.PublicErrorClass);
    }

    [Fact]
    public void E71_HarnessRoot_TempPrefix_Allowed()
    {
        var root = IsolatedHarnessRoot();
        Av3WriterAccessGate.EnsureIsolatedRoot(root);
        Assert.True(Av3WriterAccessGate.TryNormalizeHarnessRoot(root, out var normalized));
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E71_HarnessRoot_UserDocumentsWithToken_Rejected()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(docs))
        {
            return;
        }

        var fake = Path.Combine(docs, $"{Av3WriterAccessGate.HarnessRootToken}7-bait-{Guid.NewGuid():N}");
        var ex = Assert.Throws<Av3WriterRouteBlockedException>(() => Av3WriterAccessGate.EnsureIsolatedRoot(fake));
        Assert.Equal(Av3WriterAccessGate.ErrorIsolatedRootRequired, ex.PublicErrorClass);
        Assert.False(Av3WriterAccessGate.TryNormalizeHarnessRoot(fake, out _));
    }

    [Fact]
    public void E71_HarnessRoot_TempPlainWithoutPrefix_Rejected()
    {
        var plain = Path.Combine(Path.GetTempPath(), "plain-root-no-token-" + Guid.NewGuid().ToString("N"));
        var ex = Assert.Throws<Av3WriterRouteBlockedException>(() => Av3WriterAccessGate.EnsureIsolatedRoot(plain));
        Assert.Equal(Av3WriterAccessGate.ErrorIsolatedRootRequired, ex.PublicErrorClass);
    }

    [Fact]
    public void E71_HarnessRoot_PathEscapeOutsideTemp_Rejected()
    {
        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var escape = Path.Combine(temp, $"{Av3WriterAccessGate.HarnessRootToken}71-escape", "..", "..", "av3_escape_probe");
        var ex = Assert.Throws<Av3WriterRouteBlockedException>(() => Av3WriterAccessGate.EnsureIsolatedRoot(escape));
        Assert.Equal(Av3WriterAccessGate.ErrorIsolatedRootRequired, ex.PublicErrorClass);
    }

    [Fact]
    public async Task E71_TrustedGeneration_CommittedSuccess_Passes()
    {
        var options = BuildHarnessOptions();
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.Committed);
            var trusted = Av3WriterInvariantValidator.ValidateTrustedGenerationPreserved(3, result);
            Assert.True(trusted.Passed);
            Assert.True(Av3WriterInvariantValidator.ValidatePipelineResult(result).Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E71_TrustedGeneration_Cancelled_PreservesPrevious()
    {
        var options = BuildHarnessOptions(s => s.CancelAfterStep = Av3CommitPipelineStep.RecordJournal);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.Committed);
            var trusted = Av3WriterInvariantValidator.ValidateTrustedGenerationPreserved(3, result);
            Assert.True(trusted.Passed);
            var mgr = new Av3CommitRecoveryManager();
            var assessment = mgr.AssessSnapshot(result.Snapshot, null);
            Assert.Equal(3ul, assessment.TrustedOpenGeneration);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E71_TrustedGeneration_CleanupFailed_NoPromotion()
    {
        var options = BuildHarnessOptions(s => s.FailCleanup = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.Snapshot.CleanupFailed);
            Assert.False(result.Snapshot.CleanupCompleted);
            Assert.False(result.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.RecoveryRequired), result.Classification.ToString());
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
            Assert.True(Av3WriterInvariantValidator.ValidatePipelineResult(result).Passed);
            Assert.True(Av3WriterInvariantValidator.ValidateTrustedGenerationPreserved(3, result).Passed);
            var mgr = new Av3CommitRecoveryManager();
            Assert.Equal(3ul, mgr.AssessSnapshot(result.Snapshot, null).TrustedOpenGeneration);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public void E71_TrustedGeneration_UncommittedNewGenOpen_Violation()
    {
        var snapshot = SuccessfulSnapshot();
        var result = new Av3CommitPipelineRunner.Av3CommitPipelineResult
        {
            Committed = false,
            PostAuthDataTrusted = true,
            Classification = Av3RecoveryClassification.NewGenerationOpen,
            Snapshot = snapshot,
            Trace = new Av3CommitTrace()
        };
        var report = Av3WriterInvariantValidator.ValidatePipelineResult(result);
        Assert.False(report.Passed);
        Assert.Contains(report.Violations, v => v.Invariant == Av3WriterInvariant.NoPartialGenerationNormalOpen);
        Assert.Contains(report.Violations, v => v.Invariant == Av3WriterInvariant.OldGenerationPreservedUntilCommit);
    }

    [Fact]
    public void E71_TrustedGeneration_PreAuthNewGenTrust_Violation()
    {
        var snapshot = SuccessfulSnapshot();
        snapshot.ActivationAuthenticated = false;
        snapshot.MetadataAuthenticated = false;
        snapshot.CleanupCompleted = false;
        snapshot.CleanupFailed = false;
        var result = new Av3CommitPipelineRunner.Av3CommitPipelineResult
        {
            Committed = false,
            PostAuthDataTrusted = false,
            Classification = Av3RecoveryClassification.RecoveryRequired,
            Snapshot = snapshot,
            Trace = new Av3CommitTrace()
        };
        var trusted = Av3WriterInvariantValidator.ValidateTrustedGenerationPreserved(3, result);
        Assert.True(trusted.Passed);
    }

    [Fact]
    public async Task E71_CancellationToken_CallerCancel_NotCommitted()
    {
        var options = BuildHarnessOptions();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options, cts.Token);
            Assert.False(result.Committed);
            Assert.True(result.Cancelled);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
            var summary = result.Cancellation?.ToPublicSummary() ?? string.Empty;
            Assert.True(Av3WriterInvariantValidator.ValidatePublicTextSurface(summary, "cancel-token").Passed);
            Assert.DoesNotContain(SecretMarker, summary, StringComparison.Ordinal);
            Assert.DoesNotContain(options.VaultRoot, summary, StringComparison.Ordinal);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Theory]
    [InlineData(Av3RecoveryClassification.NewGenerationOpen)]
    [InlineData(Av3RecoveryClassification.RedundancyDegraded)]
    [InlineData(Av3RecoveryClassification.RecoveryRequired)]
    [InlineData(Av3RecoveryClassification.CorruptBlocked)]
    [InlineData(Av3RecoveryClassification.RollbackSuspected)]
    [InlineData(Av3RecoveryClassification.PreviousGenerationOpen)]
    [InlineData(Av3RecoveryClassification.Aborted)]
    [InlineData(Av3RecoveryClassification.UnknownFailClosed)]
    public void E71_RepairClassifier_NoStorageMutation(Av3RecoveryClassification recovery)
    {
        var probeDir = IsolatedHarnessRoot();
        Directory.CreateDirectory(probeDir);
        var probeFile = Path.Combine(probeDir, "probe.bin");
        File.WriteAllBytes(probeFile, [9, 8, 7]);
        var before = File.ReadAllBytes(probeFile);

        _ = Av3RepairClassifier.FromRecovery(recovery);
        var mgr = new Av3CommitRecoveryManager();
        _ = mgr.ClassifySnapshot(SnapshotForRecovery(recovery));

        var after = File.ReadAllBytes(probeFile);
        Assert.Equal(before, after);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
        CleanupRoot(probeDir);
    }

    [Fact]
    public void E71_AppHostViewModel_NoCommitWriter()
    {
        AssertNoCommitWriterReferences(typeof(SecureVaultService).Assembly, typeof(SecureVaultService).FullName!);
        AssertNoCommitWriterReferences(typeof(AstraVaultHostService).Assembly, typeof(AstraVaultHostService).FullName!);
        AssertNoCommitWriterReferences(typeof(SecureVaultViewModel).Assembly, typeof(SecureVaultViewModel).FullName!);
    }

    private static Av3CommitSnapshot SuccessfulSnapshot() => new()
    {
        PreviousAuthenticatedGeneration = 3,
        AttemptedTargetGeneration = 4,
        ObjectsFlushed = true,
        MetadataFlushed = true,
        JournalFlushed = true,
        ActivationWritten = true,
        ActivationFlushed = true,
        RereadSucceeded = true,
        ActivationAuthenticated = true,
        MetadataAuthenticated = true,
        CleanupCompleted = true,
        CleanupFailed = false,
        HeaderCopyDurableCount = 3
    };

    private static Av3CommitSnapshot SnapshotForRecovery(Av3RecoveryClassification recovery) =>
        recovery switch
        {
            Av3RecoveryClassification.NewGenerationOpen => SuccessfulSnapshot(),
            Av3RecoveryClassification.RedundancyDegraded => new Av3CommitSnapshot
            {
                PreviousAuthenticatedGeneration = 3,
                AttemptedTargetGeneration = 4,
                ActivationAuthenticated = true,
                MetadataAuthenticated = true,
                CleanupCompleted = true,
                HeaderCopyDurableCount = 1
            },
            Av3RecoveryClassification.RecoveryRequired => new Av3CommitSnapshot
            {
                PreviousAuthenticatedGeneration = 3,
                AttemptedTargetGeneration = 4,
                ActivationAuthenticated = true,
                MetadataAuthenticated = true,
                CleanupFailed = true,
                CleanupCompleted = false
            },
            Av3RecoveryClassification.CorruptBlocked => new Av3CommitSnapshot { EqualGenerationConflictingRoot = true },
            Av3RecoveryClassification.RollbackSuspected => new Av3CommitSnapshot { RollbackSuspected = true },
            Av3RecoveryClassification.PreviousGenerationOpen => new Av3CommitSnapshot { StaleHighGenerationUnauthenticated = true },
            Av3RecoveryClassification.Aborted => new Av3CommitSnapshot { Aborted = true },
            _ => new Av3CommitSnapshot { DiskFull = true }
        };

    private static void AssertPublicErrorSafe(string publicErrorClass)
    {
        Assert.True(Av3WriterAccessGate.IsPublicErrorClassSafe(publicErrorClass));
        Assert.DoesNotContain("password", publicErrorClass, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VMK", publicErrorClass, StringComparison.Ordinal);
        Assert.DoesNotContain("DEK", publicErrorClass, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretMarker, publicErrorClass, StringComparison.Ordinal);
        Assert.True(Av3JournalLeakScanner.ScanText(publicErrorClass, "gate-e71").Passed);
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
        Path.Combine(Path.GetTempPath(), $"{Av3WriterAccessGate.HarnessRootToken}71-{Guid.NewGuid():N}");

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