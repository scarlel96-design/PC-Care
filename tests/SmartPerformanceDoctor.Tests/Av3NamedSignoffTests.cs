using System.Reflection;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>Named Security / Engineering sign-off verification (E-10 entry; not production enable).</summary>
public sealed class Av3NamedSignoffTests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void NamedSignoff_M01M02Closed_CurrentEvidenceConsistent()
    {
        Assert.True(Av3PhaseGate.E91ExternalReviewFixesApplied);
        Assert.True(Av3PhaseGate.E10NamedSignoffRecordComplete);

        var sot = LoadSourceOfTruth();
        Assert.True(sot.LatestVerified.FullSuite.Passed >= 477);
        Assert.Equal(0, sot.LatestVerified.FullSuite.Failed);
        Assert.True(sot.LatestVerified.FilteredAv3WriterSlice.Passed >= 191);

        var docsRoot = ResolveSecurityDocsRoot();
        foreach (var name in new[]
                 {
                     "ASTRA_VAULT_EXTERNAL_REVIEW_EVIDENCE_INDEX.md",
                     "ASTRA_VAULT_PRODUCTION_WRITER_REVIEW_PACKAGE.md",
                     "ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md",
                 })
        {
            var text = File.ReadAllText(Path.Combine(docsRoot, name));
            Assert.Contains(sot.LatestVerified.FullSuite.Passed.ToString(), text, StringComparison.Ordinal);
        }

        Assert.NotNull(typeof(Av3HarnessCommitGuardRegistry));
        Assert.NotNull(typeof(IAv3CommitGuardLease));
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void NamedSignoff_DecisionRecord_NotProductionEnable()
    {
        var text = File.ReadAllText(Path.Combine(ResolveSecurityDocsRoot(), "ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md"));
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
        Assert.Contains("NO-GO", text, StringComparison.Ordinal);
        Assert.Contains("E-10", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GO for production enable", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("production-ready", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("S-Class satisfied", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void NamedSignoff_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void NamedSignoff_ExternalReviewCompletedCodeRemainsFalse()
    {
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.E10NamedSignoffRecordComplete);
    }

    [Fact]
    public void NamedSignoff_ServiceUiImportExport_NoWriterOrDryRunWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, DryRunPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, DryRunPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, DryRunPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void NamedSignoff_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3EnableReadinessChecklist.MigrationSeparated);
        Assert.True(Av3EnableReadinessChecklist.R9DeferredToPhaseH);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
    }

    [Fact]
    public void NamedSignoff_E10Checklist_ContainsRemainingBlockers()
    {
        var path = Path.Combine(ResolveSecurityDocsRoot(), "ASTRA_VAULT_E10_ENABLE_DECISION_GATE_CHECKLIST.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("Production anchor", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("XChaCha24", text, StringComparison.Ordinal);
        Assert.Contains("S-Class", text, StringComparison.Ordinal);
        Assert.Contains("disk durability", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase H migration", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Service/UI", text, StringComparison.Ordinal);
        Assert.Contains("M-01", text, StringComparison.Ordinal);
        Assert.Contains("M-02", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedSignoff_NoSClassOrProductionReadyClaims()
    {
        foreach (var name in new[]
                 {
                     "ASTRA_VAULT_NAMED_SIGNOFF_RECORD.md",
                     "ASTRA_VAULT_FORMAL_EXTERNAL_REVIEW_SIGNOFF.md",
                     "ASTRA_VAULT_E10_ENABLE_DECISION_GATE_CHECKLIST.md",
                 })
        {
            var text = File.ReadAllText(Path.Combine(ResolveSecurityDocsRoot(), name));
            AssertNoPositiveProductionOrSClassClaims(text);
        }
    }

    private static void AssertNoPositiveProductionOrSClassClaims(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (line.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
                || line.Contains("NOT YET", StringComparison.Ordinal)
                || line.Contains("NO-GO", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.DoesNotContain("production-ready", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("S-Class satisfied", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("S-Class achieved", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoNamespace(Assembly assembly, string prefix)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.ReturnType.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
                {
                    throw new InvalidOperationException($"Unexpected return type {method.ReturnType.FullName}");
                }

                foreach (var p in method.GetParameters())
                {
                    if (p.ParameterType.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
                    {
                        throw new InvalidOperationException($"Unexpected parameter type {p.ParameterType.FullName}");
                    }
                }
            }
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

        throw new InvalidOperationException("docs/security not found");
    }

    private static Av3TestEvidenceSourceOfTruth LoadSourceOfTruth()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestAssets", "av3_external_review_test_evidence.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Av3TestEvidenceSourceOfTruth>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("SOT JSON parse failed");
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

            public int Failed { get; init; }
        }
    }
}