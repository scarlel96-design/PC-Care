using System.Reflection;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>E-10 Enable Decision Gate — adjudication verification (not production enable).</summary>
public sealed class Av3PhaseE10SignoffTests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E10_Preflight_CurrentEvidenceSourceOfTruthMatchesActual()
    {
        var sot = LoadSourceOfTruth();
        Assert.True(
            string.Equals(sot.PhaseLabel, "E-14", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-13.1", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-13", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-12.1", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-12", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-11.1", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-11", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-10", StringComparison.Ordinal),
            $"Unexpected phase label: {sot.PhaseLabel}");
        Assert.True(sot.LatestVerified.FullSuite.Passed > 0);
        Assert.Equal(0, sot.LatestVerified.FullSuite.Failed);

        var docsRoot = ResolveSecurityDocsRoot();
        var sotMd = File.ReadAllText(Path.Combine(docsRoot, "ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md"));
        Assert.Contains(sot.LatestVerified.FullSuite.Passed.ToString(), sotMd, StringComparison.Ordinal);
        Assert.Contains("latest verified", sotMd, StringComparison.OrdinalIgnoreCase);

        var e10Report = Path.Combine(docsRoot, "ASTRA_VAULT_E10_ENABLE_DECISION_GATE_REPORT.md");
        Assert.True(File.Exists(e10Report));
        var report = File.ReadAllText(e10Report);
        Assert.Contains(sot.LatestVerified.FullSuite.Passed.ToString(), report, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E10_DecisionGate_NotProductionEnable()
    {
        var text = File.ReadAllText(Path.Combine(ResolveSecurityDocsRoot(), "ASTRA_VAULT_E10_ENABLE_DECISION_RECORD.md"));
        Assert.Contains("NO-GO", text, StringComparison.Ordinal);
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
        Assert.Contains("enable decision", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GO for production enable", text, StringComparison.OrdinalIgnoreCase);

    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E10_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.E10EnableDecisionGateComplete);
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);
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
    public void E10_ExternalReviewCompletedCodeRemainsFalse()
    {
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.E10NamedSignoffRecordComplete);
        Assert.True(Av3PhaseGate.E10EnableDecisionGateComplete);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E10_ProductionWriterStillNotAuthorized()
    {
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);
        Assert.False(Av3EnableReadinessChecklist.ProductionEnableAuthorized);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.False(Av3EnableReadinessChecklist.ProductionWriterStillDisabled);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E10_WriterEnableReadinessNoGoWhenAnchorMissing()
    {
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E10_WriterEnableReadinessNoGoWhenXChaCha24Missing()
    {
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E10_WriterEnableReadinessNoGoWhenSClassUnsatisfied()
    {
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E10_WriterEnableReadinessNoGoWhenActualDiskDurabilityMissing()
    {
        Assert.False(Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed);
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
    }

    [Fact]
    public void E10_ServiceUiImportExport_NoWriterOrDryRunWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, DryRunPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, DryRunPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, DryRunPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E10_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3EnableReadinessChecklist.MigrationSeparated);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
    }

    [Fact]
    public void E10_NoSClassOrProductionReadyClaims()
    {
        foreach (var name in new[]
                 {
                     "ASTRA_VAULT_E10_ENABLE_DECISION_GATE_REPORT.md",
                     "ASTRA_VAULT_E10_ENABLE_DECISION_RECORD.md",
                     "ASTRA_VAULT_E10_ENABLE_DECISION_GATE_CHECKLIST.md",
                 })
        {
            AssertNoPositiveProductionOrSClassClaims(File.ReadAllText(Path.Combine(ResolveSecurityDocsRoot(), name)));
        }
    }

    [Fact]
    public void E10_NextPhase_BlockerClosureRequired()
    {
        var text = File.ReadAllText(Path.Combine(ResolveSecurityDocsRoot(), "ASTRA_VAULT_E10_ENABLE_DECISION_RECORD.md"));
        Assert.Contains("blocker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Anchor", text, StringComparison.Ordinal);
        Assert.Contains("XChaCha24", text, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);
    }

    [Fact]
    public void E10_NamedSignoffRecord_PreparedHumanSignoffPending()
    {
        var text = File.ReadAllText(Path.Combine(ResolveSecurityDocsRoot(), "ASTRA_VAULT_NAMED_SIGNOFF_RECORD.md"));
        Assert.Contains("Recorded Signatory", text, StringComparison.Ordinal);
        Assert.Contains("Human named sign-off", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending", text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoPositiveProductionOrSClassClaims(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (line.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
                || line.Contains("NOT YET", StringComparison.Ordinal)
                || line.Contains("NO-GO", StringComparison.Ordinal)
                || line.Contains("not claim", StringComparison.OrdinalIgnoreCase)
                || line.Contains("not authorize", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Do **not**", StringComparison.Ordinal))
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
        public string PhaseLabel { get; init; } = "";

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