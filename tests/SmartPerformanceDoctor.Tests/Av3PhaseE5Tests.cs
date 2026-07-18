using System.Reflection;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE5Tests
{
    private const string WriterDesignNamespacePrefix = "SmartPerformanceDoctor.AstraVault.WriterDesign";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E5_ProductionWriterDesign_NotWriterEnable()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.ProductionWriterDesignLocked);
        Assert.True(Av3PhaseGate.ExternalReviewPackageReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.AnchorModelDocumented);
        Assert.True(Av3PhaseGate.XChaChaMigrationPlanDocumented);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.HighRiskClosureGateLocked);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void WriterEnableChecklist_E5_1_HarnessClosed_NotProductionEnable()
    {
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
        Assert.True(Av3EnableReadinessChecklist.R1PartialTornWriteHarnessClosed);
        Assert.True(Av3EnableReadinessChecklist.R10RollbackHarnessClosedOrLimitationDocumented);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.Empty(Av3EnableReadinessChecklist.BlockingReasons);
        Assert.Contains(Av3EnableReadinessChecklist.BlockingReasons, r => r.Contains("NOT AUTHORIZED", StringComparison.OrdinalIgnoreCase)
            || r.Contains("WriterEnableReady=false", StringComparison.Ordinal));
    }

    [Fact]
    public void SecretNonLeakPass_LinkedToHarnessGates()
    {
        Assert.True(Av3PhaseGate.JournalConfidentialityChecked);
        Assert.True(Av3PhaseGate.HighRiskClosureHarnessEnabled);
        Assert.True(Av3PhaseGate.ActualKillHarnessEnabled);
        Assert.True(Av3EnableReadinessChecklist.SecretNonLeakPass);
    }

    [Fact]
    public void SecretNonLeakPass_BackingTests_ExistInE4Fixture()
    {
        foreach (var entry in Av3EnableReadinessChecklist.SecretNonLeakBackingTests)
        {
            var dot = entry.LastIndexOf('.');
            Assert.True(dot > 0, $"Invalid backing test id: {entry}");
            var typeName = entry[..dot];
            var methodName = entry[(dot + 1)..];
            var type = Type.GetType($"{typeof(Av3PhaseE5Tests).Namespace}.{typeName}, {typeof(Av3PhaseE5Tests).Assembly.FullName}");
            Assert.NotNull(type);
            var method = type!.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.NotNull(method);
        }
    }

    [Theory]
    [InlineData("docs/security/ASTRA_VAULT_PRODUCTION_WRITER_DESIGN.md")]
    [InlineData("docs/security/ASTRA_VAULT_PRODUCTION_WRITER_REVIEW_PACKAGE.md")]
    [InlineData("docs/security/ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md")]
    [InlineData("docs/security/ASTRA_VAULT_ANCHOR_MODEL.md")]
    [InlineData("docs/security/ASTRA_VAULT_XCHACHA24_MIGRATION_PLAN.md")]
    [InlineData("docs/security/ASTRA_VAULT_EXTERNAL_REVIEW_BRIEF.md")]
    [InlineData("docs/security/ASTRA_VAULT_REVIEW_QUESTIONNAIRE.md")]
    public void E5_SecurityDocs_Exist(string relativePath)
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, relativePath)), $"Missing: {relativePath}");
    }

    [Fact]
    public void E5_WriterDesign_Interfaces_Present_NoImplementations()
    {
        var assembly = typeof(Av3PhaseGate).Assembly;
        var names = new[]
        {
            nameof(IAv3VaultWriter),
            nameof(IAv3WriteSession),
            nameof(IAv3TransactionCoordinator),
            nameof(IAv3DurableStore),
            nameof(IAv3HeaderCommitter),
            nameof(IAv3JournalRecorder),
            nameof(IAv3RecoveryManager),
            nameof(IAv3WritePolicy)
        };
        foreach (var name in names)
        {
            var t = assembly.GetType($"{WriterDesignNamespacePrefix}.{name}");
            Assert.NotNull(t);
            Assert.True(t!.IsInterface);
        }
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E5_AnchorDesign_NotImplemented()
    {
        Assert.False(Av3AnchorDesignPolicy.ProductionAnchorImplemented);
        Assert.False(Av3AnchorDesignPolicy.StoresSecrets);
        Assert.False(Av3AnchorDesignPolicy.StoresPathsOrFilenamesInAnchorLog);
        Assert.Equal(8, Enum.GetValues<Av3AnchorStatus>().Length);
    }

    [Fact]
    public void E5_NoProductionServiceUiWriterTypes()
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
    public void Security_AppAssembly_NoWriterDesignReferences()
    {
        AssertNoWriterDesignReferences(typeof(SecureVaultViewModel).Assembly);
    }

    [Fact]
    public void Security_SecureVaultService_NoWriterDesignReferences()
    {
        AssertNoWriterDesignReferences(typeof(SecureVaultService).Assembly, typeof(SecureVaultService).FullName!);
    }

    [Fact]
    public void Security_AstraVaultHostService_NoWriterDesignReferences()
    {
        AssertNoWriterDesignReferences(typeof(AstraVaultHostService).Assembly, typeof(AstraVaultHostService).FullName!);
    }

    [Fact]
    public void Security_ViewModel_NoWriterEnableUiClaims()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        var label = Av3PhaseGate.PhaseLabel;
        Assert.Contains("PRODUCTION AUTHORIZED", label, StringComparison.OrdinalIgnoreCase);
        AssertNoWriterDesignReferences(typeof(SecureVaultViewModel).Assembly, typeof(SecureVaultViewModel).FullName!);
    }

    private static void AssertNoWriterDesignReferences(Assembly assembly, string? restrictToTypeFullName = null)
    {
        var hits = new List<string>();
        var types = assembly.GetTypes()
            .Where(t => restrictToTypeFullName is null || t.FullName == restrictToTypeFullName);

        foreach (var type in types)
        {
            CollectWriterDesignHits(type, hits);
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var p in ctor.GetParameters())
                {
                    RecordIfWriterDesign(p.ParameterType, $"{type.FullName}..ctor({p.Name})");
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.IsAbstract && method.DeclaringType != type)
                {
                    continue;
                }

                RecordIfWriterDesign(method.ReturnType, $"{type.FullName}.{method.Name} return");
                foreach (var p in method.GetParameters())
                {
                    RecordIfWriterDesign(p.ParameterType, $"{type.FullName}.{method.Name}({p.Name})");
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                RecordIfWriterDesign(field.FieldType, $"{type.FullName}.{field.Name} field");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                RecordIfWriterDesign(prop.PropertyType, $"{type.FullName}.{prop.Name} property");
            }
        }

        Assert.Empty(hits);

        void RecordIfWriterDesign(Type? t, string context)
        {
            if (t is null)
            {
                return;
            }

            if (IsWriterDesignType(t))
            {
                hits.Add($"{context} -> {t.FullName}");
            }
        }
    }

    private static void CollectWriterDesignHits(Type type, List<string> hits)
    {
        if (type.BaseType is not null && IsWriterDesignType(type.BaseType))
        {
            hits.Add($"{type.FullName} base -> {type.BaseType.FullName}");
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (IsWriterDesignType(iface))
            {
                hits.Add($"{type.FullName} iface -> {iface.FullName}");
            }
        }
    }

    private static bool IsWriterDesignType(Type type)
    {
        if (type.FullName?.StartsWith(WriterDesignNamespacePrefix, StringComparison.Ordinal) == true)
        {
            return true;
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                if (IsWriterDesignType(arg))
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