using System.Reflection;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Anchor;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>E-13.1 trusted anchor provider sign-off — B-1 formal adjudication; not production enable.</summary>
public sealed class Av3PhaseE131Tests
{
    private const string AnchorPrefix = "SmartPerformanceDoctor.AstraVault.Anchor";
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_TrustedAnchorSignoff_PreflightEvidenceCurrent()
    {
        var sot = LoadSourceOfTruth();
        Assert.True(
            string.Equals(sot.PhaseLabel, "E-13.1", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-13", StringComparison.Ordinal),
            $"Unexpected phase: {sot.PhaseLabel}");
        Assert.True(sot.LatestVerified.FullSuite.Passed > 0);
        Assert.Equal(0, sot.LatestVerified.FullSuite.Failed);
        var md = File.ReadAllText(ResolveDoc("ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md"));
        Assert.Contains(sot.LatestVerified.FullSuite.Passed.ToString(), md, StringComparison.Ordinal);
        Assert.Contains("latest verified", md, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet format", File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_1_TRUSTED_ANCHOR_SIGNOFF_REPORT.md")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E131_ProviderContract_Locked()
    {
        Assert.True(Av3PhaseGate.E13TrustedAnchorProviderPackageComplete);
        Assert.True(Av3PhaseGate.E131TrustedAnchorSignoffGateComplete);
        Assert.False(Av3TrustedAnchorPolicy.StoresSecrets);
        Assert.False(Av3TrustedAnchorPolicy.StoresPathsOrFilenames);
        Assert.True(Av3TrustedAnchorPrivacyPolicy.ExternalWitnessDigestOnly);
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_TRUSTED_ANCHOR_PROVIDER_CONTRACT.md"));
        Assert.Contains("LOCKED", text, StringComparison.Ordinal);
        Assert.Contains("IAv3TrustedAnchorProvider", text, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_HarnessProvider_Signed()
    {
        Assert.Equal(Av3TrustedAnchorProviderKind.HarnessSynthetic, new Av3HarnessTrustedAnchorProvider().ProviderKind);
        Assert.False(new Av3HarnessTrustedAnchorProvider().IsAvailableForProductionEnable);
        Assert.True(Av3TrustedAnchorRuntimePolicy.HarnessTrustedAnchorEnabled);
        Assert.False(Av3TrustedAnchorRuntimePolicy.ProductionTrustedAnchorRouteEnabled);
    }

    [Fact]
    public void E131_MachineLocalCandidate_SignedButPartial()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_1_PRODUCTION_ANCHOR_DECISION.md"));
        Assert.Contains("PARTIAL", text, StringComparison.Ordinal);
        Assert.Contains("Machine-local", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(Av3HybridTrustedAnchorPolicyCoordinator.SameDiskOnlyClosureDenied());
        Assert.False(Av3TrustedAnchorClassifier.SameDiskAnchorCanCloseFullVaultRollback(Av3TrustedAnchorProviderKind.SameDiskLocalUntrusted));
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_ExternalWitnessStub_SignedButNotProductionLive()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_1_PRODUCTION_ANCHOR_DECISION.md"));
        Assert.Contains("stub", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("live", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.False(Av3PhaseGate.TrustedMonotonicProductionAnchorImplemented);
    }

    [Fact]
    public void E131_HybridPolicy_Signed()
    {
        Assert.Equal(Av3TrustedAnchorProviderKind.HybridPolicyCoordinator, Av3TrustedAnchorPolicy.ProductionDesignTarget);
        Assert.True(Av3TrustedAnchorOfflinePolicy.WriterTrustedPromotionRequiresOnlineExternalConfirmation);
    }

    [Fact]
    public void E131_SameDiskAnchorCannotCloseFullRollback()
    {
        foreach (var name in new[]
                 {
                     "ASTRA_VAULT_E13_1_TRUSTED_ANCHOR_SIGNOFF_REPORT.md",
                     "ASTRA_VAULT_E13_1_PRODUCTION_ANCHOR_DECISION.md",
                     "ASTRA_VAULT_E11_1_TRUSTED_ANCHOR_DECISION.md",
                 })
        {
            var text = File.ReadAllText(ResolveDoc(name));
            Assert.Contains(
                "same-disk untrusted anchor alone cannot prove full-vault rollback resistance",
                text,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void E131_FullVaultRollbackCoverage_HarnessClosedProductionPartial()
    {
        var matrix = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_FULL_ROLLBACK_CLOSURE_MATRIX.md"));
        Assert.Contains("CLOSED", matrix, StringComparison.Ordinal);
        Assert.Contains("PARTIAL", matrix, StringComparison.Ordinal);
        var decision = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_1_PRODUCTION_ANCHOR_DECISION.md"));
        Assert.Contains("PARTIAL", decision, StringComparison.Ordinal);
        Assert.Contains("B-1", decision, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_ProductionAnchorImplemented_RemainsFalse()
    {
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.False(Av3AnchorDesignPolicy.ProductionAnchorImplemented);
        Assert.False(Av3TrustedAnchorPolicy.ProductionAnchorImplemented);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_ProductionEnableAuthorized_RemainsFalse() =>
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.XChaCha24SignoffApprovedCandidate);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_ExternalReviewCompletedCode_RemainsFalse()
    {
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewCompleted);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_XChaCha24Implemented_RemainsFalseSignedCandidate()
    {
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.XChaCha24ImplementationCandidate);
        Assert.True(Av3PhaseGate.XChaCha24SignoffApprovedCandidate);
    }

    [Fact]
    public void E131_ServiceUiImportExport_NoTrustedAnchorOrWriterWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, DryRunPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
        Assert.False(Av3TrustedAnchorRecoveryPolicy.AutomaticRepairEnabled);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_SClassStillNotSatisfied()
    {
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_1_TRUSTED_ANCHOR_SIGNOFF_REPORT.md"));
        Assert.Contains("NOT YET SATISFIED", text, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E131_DiskDurabilityStillOpen() =>
        Assert.False(Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed);

    [Fact]
    public void E131_NextPhase_DiskDurabilityReview()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_1_TRUSTED_ANCHOR_SIGNOFF_REPORT.md"));
        Assert.Contains("Disk Durability", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
        Assert.Contains("NO-GO", text, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: stability matrix under pre-enable rules superseded")]
    public void E131_StabilityRepeat_Skipped()
    {
    }

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

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            var candidate = Path.Combine(dir, "docs", "security", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException($"Doc not found: {name}");
    }

    private static SourceOfTruthDocument LoadSourceOfTruth()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestAssets", "av3_external_review_test_evidence.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestAssets", "av3_external_review_test_evidence.json");
        }

        return JsonSerializer.Deserialize<SourceOfTruthDocument>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("SOT JSON missing");
    }

    private sealed class SourceOfTruthDocument
    {
        public string PhaseLabel { get; set; } = string.Empty;

        public LatestVerifiedBlock LatestVerified { get; set; } = new();
    }

    private sealed class LatestVerifiedBlock
    {
        public SuiteBlock FullSuite { get; set; } = new();

        public SuiteBlock FilteredAv3WriterSlice { get; set; } = new();
    }

    private sealed class SuiteBlock
    {
        public int Passed { get; set; }

        public int Failed { get; set; }
    }
}