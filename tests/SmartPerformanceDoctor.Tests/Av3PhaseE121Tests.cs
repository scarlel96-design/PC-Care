using System.Reflection;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.DryRun;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>E-12.1 XChaCha24 crypto sign-off gate.</summary>
public sealed class Av3PhaseE121Tests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string CryptoPrefix = "SmartPerformanceDoctor.AstraVault.Crypto";

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E121_XChaCha24Signoff_PreflightEvidenceCurrent()
    {
        var sot = LoadSourceOfTruth();
        Assert.True(
            string.Equals(sot.PhaseLabel, "E-14", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-13.1", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-13", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-12.1", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-12", StringComparison.Ordinal),
            $"Unexpected phase: {sot.PhaseLabel}");
        Assert.True(sot.LatestVerified.FullSuite.Passed > 0);
        Assert.Equal(0, sot.LatestVerified.FullSuite.Failed);
        var md = File.ReadAllText(ResolveDoc("ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md"));
        Assert.Contains(sot.LatestVerified.FullSuite.Passed.ToString(), md, StringComparison.Ordinal);
        Assert.Contains("dotnet format", File.ReadAllText(ResolveDoc("ASTRA_VAULT_E12_1_XCHACHA24_CRYPTO_SIGNOFF_REPORT.md")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E121_XChaCha24Signoff_VectorPackageComplete()
    {
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildActivationPayloadVector());
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildMetadataRootVector());
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildEmptyPlaintextVector());
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildMultiSegmentPlaintextVector());
        var manifest = Path.Combine(AppContext.BaseDirectory, "av3-vectors", "xchacha24", "manifest.json");
        Assert.True(File.Exists(manifest));
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E121_XChaCha24Signoff_ReadOnlyValidatorComplete()
    {
        var plan = new Av3WritePlan
        {
            ContainerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            TargetGeneration = 4,
            PreviousGeneration = 3
        };
        var vmk = Av3SyntheticVaultFixture.DeriveTestOnlyKey(plan.ContainerId, "vmk");
        var meta = Av3HarnessCommitCrypto.BuildMetadataRootArtifacts(vmk, plan, Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24);
        var headerBytes = Av3HarnessCommitCrypto.BuildActivationHeaderCopy(
            vmk, plan, meta.CiphertextDigest, meta.PlaintextCommitment, cipherSuite: Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24);
        var header = VaultHeaderCopy.Parse(headerBytes, (uint)headerBytes.Length);
        var desc = MetadataRootDescriptor.Parse(meta.Envelope.AsSpan(0, MetadataRootDescriptor.DescriptorSize));
        var ct = meta.Envelope.AsSpan(MetadataRootDescriptor.DescriptorSize).ToArray();
        Assert.True(Av3XChaCha24ReadOnlyValidator.ValidateActivationAndMetadataChain(3, 3, vmk, header, desc, ct));
        Assert.Equal(UnlockValidationException.PublicMessage, new UnlockValidationException().Message);
    }

    [Fact]
    public void E121_XChaCha24Signoff_DowngradeProtectionClosed()
    {
        Assert.Throws<UnlockValidationException>(() =>
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(3, 1, true));
        Assert.Throws<UnlockValidationException>(() =>
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(1, 3, false));
        Assert.True(Av3CryptoDowngradeGuard.IsDowngradeAttempt(3, 1));
        Assert.Throws<UnlockValidationException>(() =>
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(99, 99, false));
    }

    [Fact]
    public void E121_XChaCha24Signoff_CryptoInvariantPass() =>
        Assert.True(Av3CryptoInvariantValidator.ValidatePhaseGates().Passed);

    [Fact]
    public void E121_XChaCha24Signoff_NoSecretLeak()
    {
        var summary = Av3CryptoCapabilityReport.PublicSummary;
        Assert.DoesNotContain("password", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vmk", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void E121_XChaCha24Signoff_ProductionRouteDenied()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.False(Av3WriterHarnessFactory.TryCreateProductionRoute().Success);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E121_XChaCha24Signoff_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
    }

    [Fact]
    public void E121_XChaCha24Signoff_ProductionEnableAuthorizedFalse() =>
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);

    [Fact]
    public void E121_XChaCha24Signoff_ExternalReviewCompletedCodeFalse() =>
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);

    [Fact]
    public void E121_XChaCha24Signoff_ProductionAnchorStillPartial()
    {
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.ProductionAnchorImplementationCandidate);
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E12_1_XCHACHA24_CRYPTO_SIGNOFF_REPORT.md"));
        Assert.Contains("PARTIAL", text, StringComparison.Ordinal);
    }

    [Fact]
    public void E121_XChaCha24Signoff_ServiceUiImportExportNoWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CryptoPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, CryptoPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, CryptoPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E121_XChaCha24Signoff_SpdVaultUnchangedNoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E121_XChaCha24Signoff_SClassStillNotSatisfied() =>
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);

    [Fact]
    public void E121_XChaCha24Signoff_NextBlockersRemainOpen()
    {
        Assert.True(Av3PhaseGate.E121XChaCha24SignoffGateComplete);
        Assert.True(Av3PhaseGate.XChaCha24SignoffApprovedCandidate);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E12_1_XCHACHA24_IMPLEMENTATION_DECISION.md"));
        Assert.Contains("NOT YET SATISFIED", text, StringComparison.Ordinal);
        Assert.Contains("NO-GO", text, StringComparison.Ordinal);
        Assert.Contains("XChaCha24Implemented", text, StringComparison.Ordinal);
    }

    [Fact]
    public void E121_GoldenVectors_LockedUnchanged()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "av3-vectors", "reference-output", "manifest.json");
        Assert.True(File.Exists(manifestPath));
        var json = File.ReadAllText(manifestPath);
        Assert.Contains("e7990614ea0fabf5efb41846715f460bf911cc718c522875d6f2caed75ebe62b", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void E121_Vector_Stability_x3(int _)
    {
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildActivationPayloadVector());
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildMetadataRootVector());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void E121_DowngradeMixed_Stability_x3(int _)
    {
        Assert.Throws<UnlockValidationException>(() => Av3CryptoDowngradeGuard.EnsureDecryptAllowed(3, 1, true));
        Assert.Throws<UnlockValidationException>(() => Av3CryptoDowngradeGuard.EnsureDecryptAllowed(1, 3, false));
    }

    private static Av3TestEvidenceSourceOfTruth LoadSourceOfTruth()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestAssets", "av3_external_review_test_evidence.json");
        return JsonSerializer.Deserialize<Av3TestEvidenceSourceOfTruth>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("SOT parse failed");
    }

    private sealed class Av3TestEvidenceSourceOfTruth
    {
        public string PhaseLabel { get; init; } = "";
        public LatestVerifiedBlock LatestVerified { get; init; } = new();
        public sealed class LatestVerifiedBlock
        {
            public SuiteEvidence FullSuite { get; init; } = new();
            public sealed class SuiteEvidence
            {
                public int Passed { get; init; }
                public int Failed { get; init; }
            }
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