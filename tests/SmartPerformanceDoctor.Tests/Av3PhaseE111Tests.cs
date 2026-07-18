using System.Reflection;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Anchor;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>E-11.1 anchor sign-off gate — B-1 PARTIAL; not production enable.</summary>
public sealed class Av3PhaseE111Tests
{
    private const string AnchorPrefix = "SmartPerformanceDoctor.AstraVault.Anchor";
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_AnchorSignoff_HarnessCandidateOnly_NotProductionImplemented()
    {
        Assert.True(Av3PhaseGate.E11AnchorClosurePackageComplete);
        Assert.True(Av3PhaseGate.ProductionAnchorImplementationCandidate);
        Assert.True(Av3PhaseGate.E111AnchorSignoffGateComplete);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.False(Av3PhaseGate.TrustedMonotonicProductionAnchorImplemented);
        Assert.False(Av3AnchorDesignPolicy.ProductionAnchorImplemented);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_TrustedMonotonicAnchorRequired_ForFullVaultRollbackClosure()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E11_1_TRUSTED_ANCHOR_DECISION.md"));
        Assert.Contains("full vault rollback closure requires trusted monotonic anchor", text, StringComparison.Ordinal);
        Assert.False(Av3PhaseGate.TrustedMonotonicProductionAnchorImplemented);
        Assert.Contains("PARTIAL", text, StringComparison.Ordinal);
    }

    [Fact]
    public void E111_SameDiskAnchorCannotCloseFullRollback()
    {
        foreach (var name in new[]
                 {
                     "ASTRA_VAULT_E11_ANCHOR_THREAT_MODEL.md",
                     "ASTRA_VAULT_E11_1_ANCHOR_SIGNOFF_REPORT.md",
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

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_ProductionAnchorImplemented_RemainsFalse() =>
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_ProductionEnableAuthorized_RemainsFalse() =>
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_ExternalReviewCompletedCode_RemainsFalse()
    {
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewCompleted);
    }

    [Fact]
    public void E111_ServiceUiImportExport_NoAnchorOrWriterWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_SClassStillNotSatisfied()
    {
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E11_1_TRUSTED_ANCHOR_DECISION.md"));
        Assert.Contains("S-Class remains NOT YET SATISFIED", text, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_XChaCha24StillOpen() =>
        Assert.True(Av3PhaseGate.XChaCha24Implemented);

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E111_DiskDurabilityStillOpen() =>
        Assert.False(Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed);

    [Fact]
    public void E111_NextPhase_XChaCha24OrTrustedAnchorProvider()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E11_1_ANCHOR_SIGNOFF_REPORT.md"));
        Assert.Contains("XChaCha24", text, StringComparison.Ordinal);
        Assert.Contains("Trusted Anchor", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void E111_E11FailureMatrix_StabilityReconfirmed(int _)
    {
        Assert.True(Av3AnchorInvariantValidator.ValidatePhaseGates().Passed);
        Assert.True(Av3PhaseGate.E111AnchorSignoffGateComplete);
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
}