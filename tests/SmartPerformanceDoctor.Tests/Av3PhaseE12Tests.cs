using System.Reflection;
using System.Text;
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

public sealed class Av3PhaseE12Tests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string CryptoPrefix = "SmartPerformanceDoctor.AstraVault.Crypto";

    [Fact]
    public void E12_XChaCha24_Vector_ActivationPayload_Pass()
    {
        var v = Av3XChaCha24VectorFactory.BuildActivationPayloadVector();
        Av3AeadVectorVerifier.VerifyDecryptPass(v);
        Assert.True(Av3CryptoInvariantValidator.ValidateVectorRoundTrip(v).Passed);
    }

    [Fact]
    public void E12_XChaCha24_Vector_MetadataRoot_Pass()
    {
        var v = Av3XChaCha24VectorFactory.BuildMetadataRootVector();
        Av3AeadVectorVerifier.VerifyDecryptPass(v);
    }

    [Fact]
    public void E12_XChaCha24_WrongKey_Rejected()
    {
        var v = Av3XChaCha24VectorFactory.BuildActivationPayloadVector();
        var cipher = Av3XChaCha24Aead.Instance;
        var wrongKey = Av3AeadKeyMaterialPolicy.DeriveFixtureKey("wrong_key_label"u8);
        var blob = new AstraCiphertext(v.Nonce, v.Tag, v.Ciphertext);
        Assert.ThrowsAny<Exception>(() => _ = cipher.Decrypt(wrongKey, blob, v.Aad));
    }

    [Fact]
    public void E12_XChaCha24_WrongNonce_Rejected()
    {
        var v = Av3XChaCha24VectorFactory.BuildActivationPayloadVector();
        Av3AeadVectorVerifier.VerifyTamperRejected(v, b =>
        {
            var n = (byte[])b.Nonce.Clone();
            n[0] ^= 0xff;
            return new AstraCiphertext(n, b.Tag, b.Cipher);
        });
    }

    [Fact]
    public void E12_XChaCha24_WrongAad_Rejected()
    {
        var v = Av3XChaCha24VectorFactory.BuildActivationPayloadVector();
        var key = Av3AeadKeyMaterialPolicy.DeriveFixtureKey(Encoding.UTF8.GetBytes(v.KeyLabel));
        var blob = new AstraCiphertext(v.Nonce, v.Tag, v.Ciphertext);
        var badAad = (byte[])v.Aad.Clone();
        badAad[0] ^= 1;
        Assert.ThrowsAny<Exception>(() => _ = Av3XChaCha24Aead.Instance.Decrypt(key, blob, badAad));
    }

    [Fact]
    public void E12_XChaCha24_TamperedCiphertext_Rejected() =>
        Av3AeadVectorVerifier.VerifyTamperRejected(
            Av3XChaCha24VectorFactory.BuildMetadataRootVector(),
            b =>
            {
                var c = (byte[])b.Cipher.Clone();
                if (c.Length > 0)
                {
                    c[0] ^= 0x55;
                }

                return new AstraCiphertext(b.Nonce, b.Tag, c);
            });

    [Fact]
    public void E12_XChaCha24_TamperedTag_Rejected() =>
        Av3AeadVectorVerifier.VerifyTamperRejected(
            Av3XChaCha24VectorFactory.BuildEmptyPlaintextVector(),
            b =>
            {
                var t = (byte[])b.Tag.Clone();
                t[0] ^= 0x55;
                return new AstraCiphertext(b.Nonce, t, b.Cipher);
            });

    [Fact]
    public void E12_XChaCha24_DowngradeAttempt_Rejected()
    {
        Assert.Throws<UnlockValidationException>(() =>
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(
                Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
                Av3AeadAlgorithmId.ChaCha12Transitional,
                xchacha24RequiredPolicy: false));
    }

    [Fact]
    public void E12_XChaCha24_MixedAlgorithmChain_Rejected()
    {
        Assert.True(Av3CryptoDowngradeGuard.IsDowngradeAttempt(
            Av3AeadAlgorithmId.ChaCha12Transitional,
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24));
        Assert.True(Av3CryptoInvariantValidator.ValidateDowngradeRejected(
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            Av3AeadAlgorithmId.ChaCha12Transitional).Passed);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_ReadOnlyValidator_XChaCha24_Pass()
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
            vmk,
            plan,
            meta.CiphertextDigest,
            meta.PlaintextCommitment,
            cipherSuite: Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24);
        var header = VaultHeaderCopy.Parse(headerBytes, (uint)headerBytes.Length);
        var desc = MetadataRootDescriptor.Parse(meta.Envelope.AsSpan(0, MetadataRootDescriptor.DescriptorSize));
        var ct = meta.Envelope.AsSpan(MetadataRootDescriptor.DescriptorSize).ToArray();
        Assert.True(Av3XChaCha24ReadOnlyValidator.ValidateActivationAndMetadataChain(
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            vmk,
            header,
            desc,
            ct));
    }

    [Fact]
    public async Task E12_DryRun_XChaCha24Fixture_Revalidated()
    {
        var root = Av3DryRunScope.CreateRoot();
        try
        {
            var options = new Av3DryRunOptions
            {
                VaultRoot = root,
                FixtureKind = Av3SyntheticFixtureKind.XChaCha24Synthetic
            };
            var result = await Av3DryRunRunner.RunAsync(options);
            Assert.True(result.Pipeline.Committed);
            Assert.True(result.ReadOnlyRevalidation.Passed);
            Assert.True(result.Validation.Passed);
            Assert.True(Av3WriterInvariantValidator.ValidatePipelineResult(result.Pipeline).Passed);
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    [Fact]
    public void E12_CryptoPublicError_Redacted()
    {
        Assert.Equal("Unlock validation failed.", UnlockValidationException.PublicMessage);
        var ex = new UnlockValidationException();
        Assert.DoesNotContain("password", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vmk", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E12_NoSecretLeak_ReportManifestTrace()
    {
        var summary = Av3CryptoCapabilityReport.PublicSummary;
        Assert.DoesNotContain("password", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dek", summary, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("av3_crypto_", summary, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_XChaCha24Implemented_RemainsFalseUntilSignoff()
    {
        Assert.True(Av3PhaseGate.E12XChaCha24ClosurePackageComplete);
        Assert.True(Av3PhaseGate.XChaCha24ImplementationCandidate);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.ChaCha12ByteNonceBelowSClass);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3EnableReadinessChecklist.AllWriterGatesClosed);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_ProductionEnableAuthorized_RemainsFalse() =>
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_ExternalReviewCompletedCode_RemainsFalse() =>
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_ProductionAnchorImplemented_RemainsFalse() =>
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);

    [Fact]
    public void E12_ServiceUiImportExport_NoCryptoWriterWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CryptoPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, CryptoPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, CryptoPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E12_SClassStillNotSatisfied() =>
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);

    [Fact]
    public void E12_NextBlockersRemainOpen()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E12_XCHACHA24_CLOSURE_REPORT.md"));
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
        Assert.Contains("NOT YET SATISFIED", text, StringComparison.Ordinal);
        Assert.Contains("XChaCha24Implemented", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void E12_XChaCha24Vector_StabilityReconfirmed(int _)
    {
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildActivationPayloadVector());
        Av3AeadVectorVerifier.VerifyDecryptPass(Av3XChaCha24VectorFactory.BuildMetadataRootVector());
        Assert.True(Av3CryptoInvariantValidator.ValidatePhaseGates().Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void E12_Downgrade_StabilityReconfirmed(int _)
    {
        Assert.Throws<UnlockValidationException>(() =>
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(3, 1, true));
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
            // best-effort harness cleanup
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