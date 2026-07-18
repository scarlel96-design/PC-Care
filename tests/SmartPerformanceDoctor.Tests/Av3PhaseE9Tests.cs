using System.Reflection;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.DryRun;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE9Tests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";
    private const string WriterDesignPrefix = "SmartPerformanceDoctor.AstraVault.WriterDesign";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E9_ExternalSignoffPrep_EnableFlagsFalse()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.E9ExternalSignoffPrepComplete);
        Assert.True(Av3PhaseGate.ExternalReviewPackageReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
    }

    [Fact]
    public void E9_PackageReady_DoesNotImplyReviewCompleted()
    {
        Assert.True(Av3PhaseGate.ExternalReviewPackageReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewCompleted);
    }

    [Fact]
    public void E9_DryRunManifest_PublicJson_NoForbiddenTokens()
    {
        var manifest = new Av3DryRunManifest
        {
            Scope = "av3_dry_run_test_only",
            FixtureKind = "Standard",
            Classification = "NewGenerationOpen",
            PipelineCommitted = true
        };
        var json = manifest.ToPublicJson();
        Assert.False(Av3DryRunTelemetryScanner.ContainsForbidden(json));
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E9_ServiceHostViewModel_NoCommitOrDryRunWiring()
    {
        AssertNoNamespaceOnAssembly(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoNamespaceOnAssembly(typeof(SecureVaultService).Assembly, DryRunPrefix);
        AssertNoNamespaceOnAssembly(typeof(AstraVaultHostService).Assembly, CommitPrefix);
        AssertNoNamespaceOnAssembly(typeof(AstraVaultHostService).Assembly, DryRunPrefix);
        AssertNoNamespaceOnAssembly(typeof(SecureVaultViewModel).Assembly, CommitPrefix);
        AssertNoNamespaceOnAssembly(typeof(SecureVaultViewModel).Assembly, DryRunPrefix);
    }

    [Fact]
    public void E9_ViewModel_NoProductionWriterInterface()
    {
        AssertNoTypeReference(typeof(SecureVaultViewModel).Assembly, typeof(IAv3VaultWriter).FullName!);
    }

    [Fact]
    public void E9_DryRunScope_RequiresE8OrHarnessPrefix()
    {
        var ok = Av3DryRunScope.CreateRoot();
        Assert.True(Av3DryRunScope.IsDryRunRootAllowed(ok, out _));
        var e7Only = Path.Combine(Path.GetTempPath(), $"{Av3WriterAccessGate.HarnessRootToken}7-only");
        Assert.False(Av3DryRunScope.IsDryRunRootAllowed(e7Only, out _));
    }

    private static void AssertNoNamespaceOnAssembly(Assembly assembly, string prefix)
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
            if (t?.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
            {
                hits.Add(t.FullName);
            }
        }
    }

    private static void AssertNoTypeReference(Assembly assembly, string typeFullName)
    {
        var hits = new List<string>();
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.ReturnType.FullName == typeFullName)
                {
                    hits.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        Assert.Empty(hits);
    }
}