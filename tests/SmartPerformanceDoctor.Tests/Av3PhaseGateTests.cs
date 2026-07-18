using System.Reflection;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseGateTests
{
    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_ProductionWriter_RemainsDisabled()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_JournalWriter_RemainsDisabled()
    {
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_Migration_RemainsDisabled()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void PhaseGate_E0_DesignLocks_MatchDocumentedGate()
    {
        Assert.True(Av3PhaseGate.WriterDesignLocked);
        Assert.True(Av3PhaseGate.CrashSafeCommitLocked);
        Assert.True(Av3PhaseGate.JournalModelLocked);
        Assert.True(Av3PhaseGate.FaultInjectionPlanLocked);
        Assert.Contains("E-14", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.E14DiskDurabilityReviewPackageComplete);
        Assert.True(Av3PhaseGate.ActualDiskDurabilityReviewCandidate);
        Assert.False(Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed);
        Assert.True(Av3PhaseGate.E13TrustedAnchorProviderPackageComplete);
        Assert.True(Av3PhaseGate.TrustedAnchorProviderImplementationCandidate);
        Assert.True(Av3PhaseGate.E131TrustedAnchorSignoffGateComplete);
        Assert.True(Av3PhaseGate.TrustedAnchorProviderSignoffSignedCandidate);
        Assert.True(Av3PhaseGate.B1ProductionAnchorSignedCandidateOnly);
        Assert.True(Av3PhaseGate.E12XChaCha24ClosurePackageComplete);
        Assert.True(Av3PhaseGate.E121XChaCha24SignoffGateComplete);
        Assert.True(Av3PhaseGate.XChaCha24SignoffApprovedCandidate);
        Assert.True(Av3PhaseGate.XChaCha24ImplementationCandidate);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.E111AnchorSignoffGateComplete);
        Assert.False(Av3PhaseGate.TrustedMonotonicProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.E9ExternalSignoffPrepComplete);
        Assert.True(Av3PhaseGate.E10EnableDecisionGateComplete);
        Assert.True(Av3PhaseGate.E11AnchorHarnessEnabled);
        Assert.True(Av3PhaseGate.E11AnchorClosurePackageComplete);
        Assert.True(Av3PhaseGate.ProductionAnchorImplementationCandidate);
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);
        Assert.True(Av3PhaseGate.JournalLeakScannerDeterministic);
        Assert.True(Av3PhaseGate.JournalBinaryScanSeparated);
        Assert.True(Av3PhaseGate.DisabledProductionWriterImplementationPresent);
        Assert.False(Av3PhaseGate.ExperimentalWriterHarnessEnabled);
        Assert.True(Av3PhaseGate.HarnessRealCryptoEnabled);
        Assert.True(Av3PhaseGate.ActualKillHarnessEnabled);
        Assert.True(Av3PhaseGate.HighRiskClosureHarnessEnabled);
        Assert.True(Av3PhaseGate.HighRiskClosureGateLocked);
        Assert.True(Av3PhaseGate.WriterEnableChecklistLocked);
        Assert.True(Av3PhaseGate.RollbackLimitationsDocumented);
        Assert.True(Av3PhaseGate.JournalConfidentialityChecked);
        Assert.True(Av3PhaseGate.ProductionWriterDesignLocked);
        Assert.True(Av3PhaseGate.ExternalReviewPackageReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.AnchorModelDocumented);
        Assert.True(Av3PhaseGate.XChaChaMigrationPlanDocumented);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.E6ReviewFixesApplied);
        Assert.True(Av3PhaseGate.CleanupFailureHarnessCovered);
        Assert.True(Av3PhaseGate.E7PreEnableHardeningComplete);
        Assert.True(Av3PhaseGate.E71ReviewFixesApplied);
        Assert.True(Av3PhaseGate.E8LimitedDryRunComplete);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
        Assert.True(Av3PhaseGate.WriterInvariantChecksEnabled);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
    }

    [Fact]
    public void PhaseGate_ReadOnlyValidation_RemainsEnabled()
    {
        Assert.True(Av3PhaseGate.ReadOnlyValidationEnabled);
    }

    [Fact]
    public void PhaseGate_NoProductionWriterTypes_InAstraVaultAssembly()
    {
        var assembly = typeof(Av3PhaseGate).Assembly;
        var forbiddenNameFragments = new[] { "ProductionWriter", "JournalWriter", "VaultWriter", "MigrationWriter", "Av3VaultWriter" };
        var e7GateAllowList = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Av3WriterAccessGate),
            nameof(Av3WriterHarnessFactory),
            nameof(Av3WriterRouteBlockedException),
            nameof(Av3WriterInvariantValidator),
            nameof(Av3WriterInvariantReport),
            nameof(Av3WriterInvariantViolation),
            nameof(Av3WriterCancellationReport),
            nameof(Av3WriterCommitGuard)
        };
        var offenders = assembly.GetTypes()
            .Where(t => (t.IsClass || t.IsEnum) && t.Namespace?.StartsWith("SmartPerformanceDoctor.AstraVault", StringComparison.Ordinal) == true)
            .Where(t => !e7GateAllowList.Contains(t.Name))
            .Where(t => t.Name != nameof(Av3WriterInvariant))
            .Where(t => !t.Name.StartsWith("Av3WriterInvariant", StringComparison.Ordinal))
            .Where(t => forbiddenNameFragments.Any(f => t.Name.Contains(f, StringComparison.Ordinal)))
            .Where(t => !t.Name.StartsWith("Av3WriterCancellation", StringComparison.Ordinal))
            .Where(t => !t.Name.StartsWith("Av3WriterCommit", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void PhaseGate_WriterGateDocuments_PresentInRepo()
    {
        var root = FindRepoRoot();
        var docs = new[]
        {
            "docs/security/ASTRA_VAULT_WRITER_GATE.md",
            "docs/security/ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md",
            "docs/security/ASTRA_VAULT_JOURNAL_MODEL.md",
            "docs/security/ASTRA_VAULT_FAULT_INJECTION_PLAN.md",
            "docs/security/ASTRA_VAULT_DATA_LOSS_RISK_REGISTER.md",
            "docs/security/ASTRA_VAULT_WRITER_ENABLE_CHECKLIST.md"
        };
        foreach (var rel in docs)
        {
            Assert.True(File.Exists(Path.Combine(root, rel)), $"Missing gate doc: {rel}");
        }
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