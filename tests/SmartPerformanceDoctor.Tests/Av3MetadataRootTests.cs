using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Session;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3MetadataRootTests
{
    private const string SecretMarker = "X-SECRET-MARKER-9f3c2a1b";
    private const string TestPassword = "AV3-TEST-ONLY-Password-PhaseC!";

    private static string OutputDir =>
        Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "av3-vectors")), "reference-output");

    private static byte[] ReadVector(string name) => File.ReadAllBytes(Path.Combine(OutputDir, name));

    private static byte[] MetadataEnvelope()
    {
        var desc = ReadVector("metadata-root-descriptor.bin");
        var ct = ReadVector("metadata-root-ciphertext.bin");
        var buf = new byte[desc.Length + ct.Length];
        desc.CopyTo(buf, 0);
        ct.CopyTo(buf, desc.Length);
        return buf;
    }

    private static void AssertUniformFailure(Action action, params string[] secrets)
    {
        UnlockValidationException? ex = null;
        try
        {
            action();
        }
        catch (UnlockValidationException caught)
        {
            ex = caught;
        }

        Assert.NotNull(ex);
        Assert.Equal(UnlockValidationException.PublicMessage, ex!.Message);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, ex.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Golden_MetadataRootAead_FromVectors_Pass()
    {
        var locator = ReadVector("locator.bin");
        var header = ReadVector("header-copy-0.bin");
        var result = ReadOnlyUnlockValidator.Validate(
            locator,
            [(0, header), (1, ReadVector("header-copy-1.bin")), (2, ReadVector("header-copy-2.bin"))],
            MetadataEnvelope(),
            TestPassword);
        Assert.Equal(VaultSecurityState.ReadOnlyUnlocked, result.State);
        Assert.NotNull(result.MetadataValidation);
        Assert.True(result.MetadataValidation!.Authenticated);
    }

    [Fact]
    public void Golden_MetadataRootAad_StableAgainstProductionBuilder()
    {
        var expected = ReadVector("metadata-root-aad.bin");
        var containerId = new Guid("a3f1c2e4-5b6d-4789-a012-3456789abcde");
        using var expectedDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(OutputDir, "metadata-root-expected-result.json")));
        var digest = Hex(expectedDoc.RootElement.GetProperty("metadata_ciphertext_digest_hex").GetString()!);
        var activationDigest = SHA256.HashData(ReadVector("activation-payload-plaintext.bin"));
        var built = MetadataRootAad.Build(
            3,
            1,
            containerId,
            containerId,
            42,
            42,
            digest,
            activationDigest,
            1,
            512);
        Assert.Equal(expected, built);
    }

    [Fact]
    public void Golden_MetadataRootTagTamper_Reject()
    {
        var meta = MetadataEnvelope();
        meta[116] ^= 0x33;
        AssertUniformFailure(() => Unlock(TestPassword, meta));
    }

    [Fact]
    public void Golden_MetadataRootAadTamper_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle("pw", 1);
        var header = bundle.HeaderCopy.ToArray();
        header[156] ^= 0x01;
        Assert.Throws<UnlockValidationException>(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, header)], bundle.MetadataRoot, "pw"));
    }

    [Fact]
    public void Golden_MetadataRootCiphertextDigestMismatch_Reject()
    {
        var meta = MetadataEnvelope();
        meta[72] ^= 0x44;
        AssertUniformFailure(() => Unlock(TestPassword, meta));
    }

    [Fact]
    public void Golden_MetadataRootCommitmentMismatch_Reject()
    {
        var meta = MetadataEnvelope();
        var header = ReadVector("header-copy-0.bin").ToArray();
        header[92] ^= 0x55;
        AssertUniformFailure(() =>
            ReadOnlyUnlockValidator.Validate(ReadVector("locator.bin"), [(0, header)], meta, TestPassword));
    }

    [Fact]
    public void Golden_MetadataRootGenerationRollback_Reject()
    {
        var meta = MetadataEnvelope();
        BinaryPrimitives.WriteUInt64LittleEndian(meta.AsSpan(16), 99);
        AssertUniformFailure(() => Unlock(TestPassword, meta));
    }

    [Fact]
    public void Golden_MetadataRootParentGenerationConflict_Reject()
    {
        var meta = MetadataEnvelope();
        BinaryPrimitives.WriteUInt64LittleEndian(meta.AsSpan(8), 40);
        AssertUniformFailure(() => Unlock(TestPassword, meta));
    }

    [Fact]
    public void Golden_MetadataRootUnsupportedSuite_Reject()
    {
        var plain = ReadVector("metadata-root-plaintext.bin").ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(6), 999);
        Assert.Throws<UnlockValidationException>(() =>
            MetadataRootPlaintext.ValidateCanonical(
                plain,
                VaultHeaderCopy.Parse(ReadVector("header-copy-0.bin"), 896),
                MetadataRootDescriptor.Parse(ReadVector("metadata-root-descriptor.bin"))));
    }

    [Fact]
    public void Golden_MetadataRootUnsupportedVersion_Reject()
    {
        var plain = ReadVector("metadata-root-plaintext.bin").ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(4), 99);
        Assert.Throws<UnlockValidationException>(() =>
            MetadataRootPlaintext.ValidateCanonical(
                plain,
                VaultHeaderCopy.Parse(ReadVector("header-copy-0.bin"), 896),
                MetadataRootDescriptor.Parse(ReadVector("metadata-root-descriptor.bin"))));
    }

    [Fact]
    public void Golden_MetadataRootReservedNonZero_Reject()
    {
        var plain = ReadVector("metadata-root-plaintext.bin").ToArray();
        plain[200] = 1;
        Assert.Throws<UnlockValidationException>(() =>
            MetadataRootPlaintext.ValidateCanonical(
                plain,
                VaultHeaderCopy.Parse(ReadVector("header-copy-0.bin"), 896),
                MetadataRootDescriptor.Parse(ReadVector("metadata-root-descriptor.bin"))));
    }

    [Fact]
    public void Golden_MetadataRootTrailingBytes_Reject()
    {
        var meta = MetadataEnvelope().ToList();
        meta.Add(0xEE);
        AssertUniformFailure(() => Unlock(TestPassword, meta.ToArray()));
    }

    [Fact]
    public void Golden_MetadataRootOversized_Reject()
    {
        var meta = MetadataEnvelope();
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(132), MetadataRootDescriptor.MaxCiphertextLength + 1);
        Assert.ThrowsAny<Exception>(() => MetadataRootDescriptor.Parse(meta));
    }

    [Fact]
    public void Golden_MetadataRootMalformedPlaintext_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle("pw", 1);
        var meta = bundle.MetadataRoot.ToArray();
        meta[^1] ^= 0x7F;
        AssertUniformFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, bundle.HeaderCopy)], meta, "pw"));
    }

    [Fact]
    public void Golden_WrongPassword_And_MetadataAuthFailure_SamePublicMessage()
    {
        var meta = MetadataEnvelope();
        meta[116] ^= 0x11;
        string? wrong = null;
        string? tamper = null;
        try
        {
            Unlock("wrong-password", meta);
        }
        catch (UnlockValidationException ex)
        {
            wrong = ex.Message;
        }

        try
        {
            Unlock(TestPassword, meta);
        }
        catch (UnlockValidationException ex)
        {
            tamper = ex.Message;
        }

        Assert.Equal(UnlockValidationException.PublicMessage, wrong);
        Assert.Equal(wrong, tamper);
        Assert.DoesNotContain(TestPassword, wrong ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain(SecretMarker, wrong ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Golden_MetadataVectorHashes_MatchManifest()
    {
        var manifestPath = Path.Combine(OutputDir, "manifest.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var expected = doc.RootElement.GetProperty("output_hashes");
        foreach (var name in new[]
                 {
                     "metadata-root-aad.bin",
                     "metadata-root-plaintext.bin",
                     "metadata-root-ciphertext.bin",
                     "metadata-root-tag.bin",
                     "metadata-root-commitment.bin",
                     "metadata-root-expected-result.json"
                 })
        {
            var actual = Convert.ToHexString(SHA256.HashData(ReadVector(name))).ToLowerInvariant();
            Assert.Equal(expected.GetProperty(name).GetString(), actual);
        }
    }

    [Fact]
    public void Golden_MetadataRootDeterministic_FromGenerator()
    {
        var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(OutputDir, "metadata-root-expected-result.json")));
        var commitment = Convert.ToHexString(ReadVector("metadata-root-commitment.bin")).ToLowerInvariant();
        Assert.Equal(expected.RootElement.GetProperty("root_plaintext_commitment_hex").GetString(), commitment);
        var digest = Convert.ToHexString(SHA256.HashData(ReadVector("metadata-root-ciphertext.bin"))).ToLowerInvariant();
        Assert.Equal(expected.RootElement.GetProperty("metadata_ciphertext_digest_hex").GetString(), digest);
    }

    private static void Unlock(string password, byte[] metadata) =>
        ReadOnlyUnlockValidator.Validate(
            ReadVector("locator.bin"),
            [(0, ReadVector("header-copy-0.bin"))],
            metadata,
            password);

    private static byte[] Hex(string h)
    {
        var bytes = new byte[h.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
        }

        return bytes;
    }
}