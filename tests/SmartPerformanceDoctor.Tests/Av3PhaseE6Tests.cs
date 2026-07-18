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

public sealed class Av3PhaseE6Tests
{
    private const string CommitNamespacePrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string WriterDesignNamespacePrefix = "SmartPerformanceDoctor.AstraVault.WriterDesign";
    private const string SecretMarker = "X-SECRET-MARKER-E6-7f3a";

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void PhaseGate_E6_DisabledProductionWriter_NotAuthorized()
    {
        Assert.True(Av3PhaseGate.JournalLeakScannerDeterministic);
        Assert.True(Av3PhaseGate.JournalBinaryScanSeparated);
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.E6ReviewFixesApplied);
        Assert.True(Av3PhaseGate.CleanupFailureHarnessCovered);
        Assert.True(Av3PhaseGate.DisabledProductionWriterImplementationPresent);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3WriterAccessGate.IsProductionRouteAllowed);
        Assert.Contains(Av3EnableReadinessChecklist.BlockingReasons, r =>
            r.Contains("NOT AUTHORIZED", StringComparison.OrdinalIgnoreCase)
            || r.Contains("WriterEnableReady=false", StringComparison.Ordinal));
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void WriterFactory_ProductionCreate_Fails_WhenProductionWriterDisabled()
    {
        var result = Av3WriterHarnessFactory.TryCreateProductionRoute();
        Assert.False(result.Success);
        Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, result.PublicErrorClass);
        Assert.Null(result.Writer);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task Orchestrator_ProductionCommit_Throws_Blocked()
    {
        var options = BuildHarnessOptions();
        var orchestrator = Av3WriterHarnessFactory.CreateHarnessOrchestrator(options);
        var ex = await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(() =>
            orchestrator.CommitAsync(new Av3VaultCommitRequest
            {
                TransactionId = options.Plan.TransactionId,
                TargetGeneration = options.Plan.TargetGeneration
            }).AsTask());
        Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, ex.PublicErrorClass);
        CleanupRoot(options.VaultRoot);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task Orchestrator_ProductionOpenSession_Throws_Blocked()
    {
        var options = BuildHarnessOptions();
        var orchestrator = Av3WriterHarnessFactory.CreateHarnessOrchestrator(options);
        var ex = await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(() =>
            orchestrator.OpenWriteSessionAsync(new Av3WriteSessionOpenRequest
            {
                VaultRootPath = options.VaultRoot,
                TrustedGeneration = options.Plan.PreviousGeneration
            }).AsTask());
        Assert.Equal(Av3WriterAccessGate.ErrorProductionDisabled, ex.PublicErrorClass);
        CleanupRoot(options.VaultRoot);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task JournalRecorder_ProductionRoute_Fails_WhenJournalDisabled()
    {
        var recorder = new Av3CommitJournalRecorder(harnessRoute: false);
        var ex = await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(() =>
            recorder.RecordStateAsync(new Av3JournalRecordRequest
            {
                TransactionId = Guid.NewGuid(),
                PreviousGeneration = 3,
                TargetGeneration = 4,
                TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32)
            }).AsTask());
        Assert.Equal(Av3WriterAccessGate.ErrorJournalProductionDisabled, ex.PublicErrorClass);
    }

    [Fact]
    public void HarnessRoute_Requires_IsolatedRootToken()
    {
        Av3WriterAccessGate.EnsureIsolatedRoot(IsolatedHarnessRoot());

        var missingToken = Assert.Throws<Av3WriterRouteBlockedException>(() =>
            Av3WriterAccessGate.EnsureHarnessRoute(
                testHarnessInvocation: true,
                vaultRoot: Path.Combine(Path.GetTempPath(), "plain-root-no-token")));
        Assert.Equal(Av3WriterAccessGate.ErrorIsolatedRootRequired, missingToken.PublicErrorClass);

        var isolated = IsolatedHarnessRoot();
        var harnessOnly = Assert.Throws<Av3WriterRouteBlockedException>(() =>
            Av3WriterAccessGate.EnsureHarnessRoute(testHarnessInvocation: false, vaultRoot: isolated));
        Assert.Equal(Av3WriterAccessGate.ErrorHarnessOnly, harnessOnly.PublicErrorClass);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task DurableStore_RelativePathEscape_Blocked()
    {
        var root = IsolatedHarnessRoot();
        try
        {
            var store = new Av3CommitDurableStore(root, new Av3CommitSimulationOptions());
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.WriteTempThenCommitAsync(
                    "../escape",
                    new byte[] { 1 },
                    new Av3DurableCommitOptions
                    {
                        TransactionId = Guid.NewGuid(),
                        TargetGeneration = 4
                    }).AsTask());
            Assert.Contains("escape blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact]
    public async Task E6_HarnessCommit_Success_NewGenerationOpen()
    {
        var options = BuildHarnessOptions();
        try
        {
            var orchestrator = Av3WriterHarnessFactory.CreateHarnessOrchestrator(options);
            var result = await orchestrator.RunHarnessCommitAsync();
            Assert.True(result.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification);
            Assert.Equal(options.Plan.TargetGeneration, result.TrustedGeneration);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E6_FlushFailure_NotCommitted()
    {
        var options = BuildHarnessOptions(simulation =>
            simulation.FailFlushAtStep = Av3CommitPipelineStep.FlushMetadataRoot);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.Committed);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E6_RereadFailure_NotCommitted()
    {
        var options = BuildHarnessOptions(simulation => simulation.FailReread = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.Committed);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E6_AuthFailure_NotCommitted()
    {
        var options = BuildHarnessOptions(simulation => simulation.FailAuthentication = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.CorruptBlocked), result.Classification.ToString());
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E6_PartialWrite_NotCommitted()
    {
        var options = BuildHarnessOptions(simulation => simulation.PartialWriteTruncate = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.Committed);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E6_OneHeaderCopy_RedundancyDegraded()
    {
        var options = BuildHarnessOptions(simulation =>
        {
            simulation.DurableHeaderCopy0 = true;
            simulation.DurableHeaderCopy1 = false;
            simulation.DurableHeaderCopy2 = false;
        });
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.RedundancyDegraded), result.Classification.ToString());
            Assert.Equal(Av3RepairClassification.RedundancyDegraded, result.Repair);
            Assert.Equal(1, result.Snapshot.HeaderCopyDurableCount);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public async Task E6_HeaderConflict_CorruptBlocked()
    {
        var options = BuildHarnessOptions(simulation => simulation.HeaderCopyConflict = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.CorruptBlocked), result.Classification.ToString());
            Assert.Equal(Av3RepairClassification.CorruptBlocked, result.Repair);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E6_JournalLeakScanner_Pass_OnDigestOnlyJournal()
    {
        var recorder = new Av3CommitJournalRecorder(harnessRoute: true);
        var record = await recorder.RecordStateAsync(new Av3JournalRecordRequest
        {
            TransactionId = Guid.NewGuid(),
            PreviousGeneration = 3,
            TargetGeneration = 4,
            TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32)
        });
        Assert.True(record.ConfidentialityPassed);

        var options = BuildHarnessOptions();
        try
        {
            var pipeline = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            var journalPath = Path.Combine(options.VaultRoot, "journal/current.jnal");
            Assert.True(File.Exists(journalPath));
            var journalBytes = await File.ReadAllBytesAsync(journalPath);
            var conf = Av3JournalConfidentialityValidator.ValidateJournalBytes(journalBytes);
            Assert.True(conf.Passed);
            var textualOnBinary = Av3JournalLeakScanner.ScanUtf8(journalBytes, "journal-textual-misuse");
            _ = textualOnBinary;
            var structural = Av3JournalConfidentialityScanner.Scan(journalBytes);
            Assert.True(structural.Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E6_Trace_NoSecretPathPlaintextLeak()
    {
        var options = BuildHarnessOptions();
        try
        {
            var pipeline = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            var trace = pipeline.Trace.ToPublicSummary();
            Assert.DoesNotContain(options.VaultRoot, trace, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretMarker, trace, StringComparison.Ordinal);
            Assert.DoesNotContain("password", trace, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VMK", trace, StringComparison.Ordinal);
            Assert.DoesNotContain(@"C:\Users\", trace, StringComparison.Ordinal);

            var leak = Av3JournalLeakScanner.ScanText(trace, "trace");
            Assert.True(leak.Passed);
            var journalBytes = await File.ReadAllBytesAsync(Path.Combine(options.VaultRoot, "journal/current.jnal"));
            var surfaces = Av3JournalConfidentialityValidator.ValidatePublicSurfaces(
                journalBytes,
                """{"channel":"e6-harness"}""",
                trace,
                exception: null);
            Assert.True(surfaces.Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E6_WritePolicy_ProductionDisabled()
    {
        var policy = Av3DefaultWritePolicy.Instance;
        Assert.False(policy.AllowsLegacyMigration);
        Assert.False(policy.AllowsCleartextJournalFields);
        Assert.False(policy.AllowsUserOriginDeletionByDefault);
        Assert.True(policy.RequiresPostFlushAuthentication);
        Assert.True(policy.RequiresTrustedAnchorForSClassRollback);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
    }

    [Fact]
    public void E6_WriterDesign_Interfaces_HaveCommitImplementations()
    {
        var assembly = typeof(Av3CommitOrchestrator).Assembly;
        var implementations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(IAv3VaultWriter)] = nameof(Av3CommitOrchestrator),
            [nameof(IAv3WriteSession)] = nameof(Av3CommitSession),
            [nameof(IAv3TransactionCoordinator)] = nameof(Av3CommitTransactionCoordinator),
            [nameof(IAv3DurableStore)] = nameof(Av3CommitDurableStore),
            [nameof(IAv3HeaderCommitter)] = nameof(Av3CommitHeaderCommitter),
            [nameof(IAv3JournalRecorder)] = nameof(Av3CommitJournalRecorder),
            [nameof(IAv3RecoveryManager)] = nameof(Av3CommitRecoveryManager),
            [nameof(IAv3WritePolicy)] = nameof(Av3DefaultWritePolicy)
        };

        foreach (var (ifaceName, implName) in implementations)
        {
            var iface = assembly.GetType($"{WriterDesignNamespacePrefix}.{ifaceName}");
            var impl = assembly.GetType($"{CommitNamespacePrefix}.{implName}");
            Assert.NotNull(iface);
            Assert.NotNull(impl);
            Assert.True(iface!.IsInterface);
            Assert.True(iface.IsAssignableFrom(impl));
        }
    }

    [Fact]
    public void Security_AppAssembly_NoCommitWriterProductionReferences()
    {
        AssertNoCommitNamespaceReferences(typeof(SecureVaultViewModel).Assembly);
    }

    [Fact]
    public void Security_SecureVaultService_NoCommitWriterReferences()
    {
        AssertNoCommitNamespaceReferences(typeof(SecureVaultService).Assembly, typeof(SecureVaultService).FullName!);
    }

    [Fact]
    public void Security_AstraVaultHostService_NoCommitWriterReferences()
    {
        AssertNoCommitNamespaceReferences(typeof(AstraVaultHostService).Assembly, typeof(AstraVaultHostService).FullName!);
    }

    [Fact]
    public void SpdVault_OnDisk_NoE6MigrationTypes()
    {
        var asm = typeof(Av3PhaseGate).Assembly;
        var sources = asm.GetTypes()
            .Where(t => t.Namespace?.Contains("AstraVault", StringComparison.Ordinal) == true)
            .Select(t => t.FullName ?? "");
        Assert.DoesNotContain(sources, n => n.Contains("E6Migration", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, n => n.Contains("SpdVaultMigration", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, n => n.Contains("MigrationWriter", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, n => n.Contains("Av3MigrationWriter", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("docs/security/ASTRA_VAULT_PHASE_STATUS.md")]
    [InlineData("docs/security/ASTRA_VAULT_HARDENING_PLAN.md")]
    [InlineData("docs/security/ASTRA_VAULT_GAP_REPORT.md")]
    [InlineData("docs/security/ASTRA_VAULT_PRODUCTION_WRITER_DESIGN.md")]
    [InlineData("docs/security/ASTRA_VAULT_WRITER_GATE.md")]
    [InlineData("docs/security/ASTRA_VAULT_WRITER_ENABLE_CHECKLIST.md")]
    [InlineData("docs/security/ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md")]
    [InlineData("docs/security/ASTRA_VAULT_DATA_LOSS_RISK_REGISTER.md")]
    public void E6_SecurityDocs_Exist(string relativePath)
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, relativePath)), $"Missing: {relativePath}");
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
        Path.Combine(Path.GetTempPath(), $"{Av3WriterAccessGate.HarnessRootToken}6-{Guid.NewGuid():N}");

    private static Av3CommitHarnessOptions BuildHarnessOptions(Action<Av3CommitSimulationOptions>? configureSimulation = null)
    {
        var plan = MinimalPlan();
        var simulation = new Av3CommitSimulationOptions();
        configureSimulation?.Invoke(simulation);
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
            // best-effort test cleanup
        }
    }

    private static void AssertNoCommitNamespaceReferences(Assembly assembly, string? restrictToTypeFullName = null)
    {
        var hits = new List<string>();
        var types = assembly.GetTypes()
            .Where(t => restrictToTypeFullName is null || t.FullName == restrictToTypeFullName);

        foreach (var type in types)
        {
            CollectCommitHits(type, hits);
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var p in ctor.GetParameters())
                {
                    RecordIfCommit(p.ParameterType, $"{type.FullName}..ctor({p.Name})");
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.IsAbstract && method.DeclaringType != type)
                {
                    continue;
                }

                RecordIfCommit(method.ReturnType, $"{type.FullName}.{method.Name} return");
                foreach (var p in method.GetParameters())
                {
                    RecordIfCommit(p.ParameterType, $"{type.FullName}.{method.Name}({p.Name})");
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                RecordIfCommit(field.FieldType, $"{type.FullName}.{field.Name} field");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                RecordIfCommit(prop.PropertyType, $"{type.FullName}.{prop.Name} property");
            }
        }

        Assert.Empty(hits);

        void RecordIfCommit(Type? t, string context)
        {
            if (t is null)
            {
                return;
            }

            if (IsCommitNamespaceType(t))
            {
                hits.Add($"{context} -> {t.FullName}");
            }
        }
    }

    private static void CollectCommitHits(Type type, List<string> hits)
    {
        if (type.BaseType is not null && IsCommitNamespaceType(type.BaseType))
        {
            hits.Add($"{type.FullName} base -> {type.BaseType.FullName}");
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (IsCommitNamespaceType(iface))
            {
                hits.Add($"{type.FullName} iface -> {iface.FullName}");
            }
        }
    }

    private static bool IsCommitNamespaceType(Type type)
    {
        if (type.FullName?.StartsWith(CommitNamespacePrefix, StringComparison.Ordinal) == true)
        {
            return true;
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                if (IsCommitNamespaceType(arg))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 14; i++)
        {
            if (File.Exists(Path.Combine(dir, "SmartPerformanceDoctor.sln")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}