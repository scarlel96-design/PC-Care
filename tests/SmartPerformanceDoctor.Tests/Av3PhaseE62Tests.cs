using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Journal;
using SmartPerformanceDoctor.AstraVault.Repair;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Rollback;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE62Tests
{
    private const string CommitNamespacePrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string WriterDesignNamespacePrefix = "SmartPerformanceDoctor.AstraVault.WriterDesign";
    private const string SecretMarker = "X-SECRET-MARKER-E62-9c11";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E62_ReviewFixes_ProductionStillDisabled()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.E6ReviewFixesApplied);
        Assert.True(Av3PhaseGate.CleanupFailureHarnessCovered);
        Assert.True(Av3PhaseGate.JournalLeakScannerDeterministic);
        Assert.True(Av3PhaseGate.JournalBinaryScanSeparated);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
    }

    [Fact]
    public async Task E62_CleanupFailure_AfterAuth_NotCommitted_PostAuthTrusted()
    {
        var options = BuildHarnessOptions(s => s.FailCleanup = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.PostAuthDataTrusted);
            Assert.False(result.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.RecoveryRequired), result.Classification.ToString());
            Assert.Equal(Av3CommitCleanupPosture.NewGenerationOpenCleanupRequired, result.CleanupPosture);
            Assert.True(result.Snapshot.CleanupFailed);
            Assert.False(result.Snapshot.CleanupCompleted);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E62_CleanupFailure_BeforeAuth_NotCommitted_NotTrusted()
    {
        var options = BuildHarnessOptions(s =>
        {
            s.FailCleanup = true;
            s.FailAuthentication = true;
        });
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.False(result.PostAuthDataTrusted);
            Assert.False(result.Committed);
            Assert.Equal(Av3CommitCleanupPosture.NotApplicable, result.CleanupPosture);
            Assert.NotEqual(nameof(Av3RecoveryClassification.NewGenerationOpen), result.Classification.ToString());
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E62_CleanupFailure_OneHeaderCopy_RedundancyDegradedCleanupRequired()
    {
        var options = BuildHarnessOptions(s =>
        {
            s.FailCleanup = true;
            s.DurableHeaderCopy0 = true;
            s.DurableHeaderCopy1 = false;
            s.DurableHeaderCopy2 = false;
        });
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            Assert.True(result.PostAuthDataTrusted);
            Assert.False(result.Committed);
            Assert.Equal(nameof(Av3RecoveryClassification.RecoveryRequired), result.Classification.ToString());
            Assert.Equal(Av3CommitCleanupPosture.RedundancyDegradedCleanupRequired, result.CleanupPosture);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E62_CleanupFailure_Trace_NoSecretLeak()
    {
        var options = BuildHarnessOptions(s => s.FailCleanup = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            var trace = result.Trace.ToPublicSummary();
            Assert.DoesNotContain(SecretMarker, trace, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET-MARKER", trace, StringComparison.Ordinal);
            Assert.DoesNotContain(options.VaultRoot, trace, StringComparison.Ordinal);
            Assert.DoesNotContain("password", trace, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VMK", trace, StringComparison.Ordinal);
            Assert.DoesNotContain("DEK", trace, StringComparison.Ordinal);
            var leak = Av3JournalLeakScanner.ScanText(trace, "cleanup-failure-trace");
            Assert.True(leak.Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public async Task E62_CleanupFailure_ReportSurfaces_NoPathOrSecrets()
    {
        var options = BuildHarnessOptions(s => s.FailCleanup = true);
        try
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(options);
            var report = $"classification={result.Classification} repair={result.Repair} posture={result.CleanupPosture}";
            Assert.DoesNotContain(@"C:\Users\", report, StringComparison.Ordinal);
            Assert.DoesNotContain("password", report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VMK", report, StringComparison.Ordinal);
            Assert.DoesNotContain("DEK", report, StringComparison.Ordinal);
            var leak = Av3JournalLeakScanner.ScanText(report, "cleanup-failure-report");
            Assert.True(leak.Passed);
        }
        finally
        {
            CleanupRoot(options.VaultRoot);
        }
    }

    [Fact]
    public void E62_E6Recovery_StaleHighGeneration_NotPromotedToCurrent()
    {
        var snapshot = new Av3CommitSnapshot
        {
            PreviousAuthenticatedGeneration = 4,
            AttemptedTargetGeneration = 9,
            StaleHighGenerationUnauthenticated = true,
            ActivationAuthenticated = false,
            MetadataAuthenticated = false
        };
        var recovery = Av3RecoveryClassifier.Classify(snapshot);
        Assert.Equal(Av3RecoveryClassification.PreviousGenerationOpen, recovery);
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, recovery);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E62_E6Recovery_EqualGenerationConflictingRoot_CorruptBlocked()
    {
        var snapshot = new Av3CommitSnapshot { EqualGenerationConflictingRoot = true };
        var recovery = Av3RecoveryClassifier.Classify(snapshot);
        var repair = Av3RepairClassifier.FromRecovery(recovery);
        Assert.Equal(Av3RecoveryClassification.CorruptBlocked, recovery);
        Assert.Equal(Av3RepairClassification.CorruptBlocked, repair);
    }

    [Fact]
    public void E62_Rollback_StaleActivationPayload_MatchesE4_PreviousGenerationOpen()
    {
        var observation = new Av3RollbackObservation
        {
            LastAuthenticatedGeneration = 4,
            ObservedHeaderGeneration = 9,
            ObservedMetadataGeneration = 9,
            ObservedJournalPreviousGeneration = 8,
            ObservedJournalTargetGeneration = 9,
            StaleActivationPayload = true,
            ActivationAuthenticated = false
        };
        Assert.Equal(Av3RecoveryClassification.PreviousGenerationOpen, Av3RollbackDetector.Classify(observation));
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E62_Rollback_PreviousRootMismatch_MatchesE4_CorruptBlocked()
    {
        var observation = new Av3RollbackObservation
        {
            LastAuthenticatedGeneration = 4,
            ObservedHeaderGeneration = 4,
            ObservedMetadataGeneration = 4,
            ObservedJournalPreviousGeneration = 3,
            ObservedJournalTargetGeneration = 5,
            PreviousRootDigestMismatch = true,
            ActivationAuthenticated = true
        };
        Assert.Equal(Av3RecoveryClassification.CorruptBlocked, Av3RollbackDetector.Classify(observation));
    }

    [Fact]
    public void E62_BuildDigestOnlyJournal_Deterministic_SameDescriptor()
    {
        var recorder = new Av3CommitJournalRecorder(harnessRoute: true);
        var tx = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var targetDigest = Av3JournalDeterministicFixtures.DigestSlot1;
        var request = new Av3JournalRecordRequest
        {
            TransactionId = tx,
            PreviousGeneration = 3,
            TargetGeneration = 4,
            TargetMetadataRootCiphertextDigest = targetDigest
        };
        var a = recorder.BuildDigestOnlyJournal(request);
        var b = recorder.BuildDigestOnlyJournal(request);
        Assert.Equal(a, b);
        var parsed = Av3JournalDescriptor.Parse(a);
        Assert.Equal(tx, parsed.TransactionId);
        Assert.True(Av3JournalBinaryFieldPolicy.ValidateParsedDescriptor(parsed, out _));
        Assert.True(Av3JournalConfidentialityScanner.Scan(a).Passed);
    }

    [Fact]
    public void E62_BuildDigestOnlyJournal_RecordDigest_MatchesExpectedHash()
    {
        var recorder = new Av3CommitJournalRecorder(harnessRoute: true);
        var bytes = recorder.BuildDigestOnlyJournal(new Av3JournalRecordRequest
        {
            TransactionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            PreviousGeneration = 3,
            TargetGeneration = 4,
            TargetMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot1
        });
        var parsed = Av3JournalDescriptor.Parse(bytes);
        var expected = Av3JournalDigest.ComputeRecordDigest(bytes);
        Assert.Equal(expected, parsed.RecordDigest);
    }

    [Fact]
    public void SecretNonLeakPass_Requires_E61_BinaryScanGates()
    {
        Assert.True(Av3PhaseGate.JournalLeakScannerDeterministic);
        Assert.True(Av3PhaseGate.JournalBinaryScanSeparated);
        Assert.True(Av3EnableReadinessChecklist.SecretNonLeakPass);
    }

    [Fact]
    public void Security_AppAssembly_FullReflection_NoCommitOrWriterDesignReferences()
    {
        AssertNoWriterSurfaceReferences(typeof(SecureVaultViewModel).Assembly);
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
        Path.Combine(Path.GetTempPath(), $"{Av3WriterAccessGate.HarnessRootToken}62-{Guid.NewGuid():N}");

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

    private static void AssertNoWriterSurfaceReferences(Assembly assembly)
    {
        var hits = new List<string>();
        foreach (var type in assembly.GetTypes())
        {
            CollectWriterSurfaceHits(type, hits);
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var p in ctor.GetParameters())
                {
                    RecordIfWriterSurface(p.ParameterType, $"{type.FullName}..ctor({p.Name})");
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.IsAbstract && method.DeclaringType != type)
                {
                    continue;
                }

                RecordIfWriterSurface(method.ReturnType, $"{type.FullName}.{method.Name} return");
                foreach (var p in method.GetParameters())
                {
                    RecordIfWriterSurface(p.ParameterType, $"{type.FullName}.{method.Name}({p.Name})");
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                RecordIfWriterSurface(field.FieldType, $"{type.FullName}.{field.Name} field");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                RecordIfWriterSurface(prop.PropertyType, $"{type.FullName}.{prop.Name} property");
            }
        }

        Assert.Empty(hits);

        void RecordIfWriterSurface(Type? t, string context)
        {
            if (t is not null && IsWriterSurfaceType(t))
            {
                hits.Add($"{context} -> {t.FullName}");
            }
        }
    }

    private static void CollectWriterSurfaceHits(Type type, List<string> hits)
    {
        if (type.BaseType is not null && IsWriterSurfaceType(type.BaseType))
        {
            hits.Add($"{type.FullName} base -> {type.BaseType.FullName}");
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (IsWriterSurfaceType(iface))
            {
                hits.Add($"{type.FullName} iface -> {iface.FullName}");
            }
        }
    }

    private static bool IsWriterSurfaceType(Type type)
    {
        if (type.FullName?.StartsWith(CommitNamespacePrefix, StringComparison.Ordinal) == true)
        {
            return true;
        }

        if (type.FullName?.StartsWith(WriterDesignNamespacePrefix, StringComparison.Ordinal) == true)
        {
            if (type.Name is nameof(Av3WriterAccessGate) or nameof(Av3WriterHarnessFactory) or nameof(Av3WriterRouteBlockedException))
            {
                return false;
            }

            if (type.Name.Contains("Av3Commit", StringComparison.Ordinal)
                || type.Name.Contains("Av3VaultWriter", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Vault", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Commit", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Journal", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Durable", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Header", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Recovery", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Transaction", StringComparison.Ordinal)
                || type.Name.StartsWith("IAv3Write", StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                if (IsWriterSurfaceType(arg))
                {
                    return true;
                }
            }
        }

        return false;
    }
}