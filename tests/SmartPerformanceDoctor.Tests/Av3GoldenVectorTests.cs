using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Session;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3GoldenVectorTests
{
    private static string VectorRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "av3-vectors"));

    private static string OutputDir => Path.Combine(VectorRoot, "reference-output");

    private static byte[] ReadVector(string name) => File.ReadAllBytes(Path.Combine(OutputDir, name));

    private static byte[] ReadMetadataEnvelope()
    {
        var desc = ReadVector("metadata-root-descriptor.bin");
        var ct = ReadVector("metadata-root-ciphertext.bin");
        var buf = new byte[desc.Length + ct.Length];
        desc.CopyTo(buf, 0);
        ct.CopyTo(buf, desc.Length);
        return buf;
    }

    [Fact]
    public void Golden_GeneratedLocator_ParsePass()
    {
        var locator = ReadVector("locator.bin");
        var parsed = VaultLocator.Parse(locator);
        Assert.Equal(new Guid("a3f1c2e4-5b6d-4789-a012-3456789abcde"), parsed.ContainerId);
    }

    [Fact]
    public void Golden_HeaderThreeCopyCandidates_ParsePass()
    {
        var locator = VaultLocator.Parse(ReadVector("locator.bin"));
        foreach (var name in new[] { "header-copy-0.bin", "header-copy-1.bin", "header-copy-2.bin" })
        {
            var copy = VaultHeaderCopy.Parse(ReadVector(name), locator.HeaderCopySize);
            Assert.Equal(locator.ContainerId, copy.ContainerId);
        }
    }

    [Fact]
    public void Golden_VmkUnwrap_FromHeaderSlot_Pass()
    {
        var input = LoadInput();
        var locator = VaultLocator.Parse(ReadVector("locator.bin"));
        var copy = VaultHeaderCopy.Parse(ReadVector("header-copy-0.bin"), locator.HeaderCopySize);
        var slot = copy.PasswordSlots[0];
        var kek = AstraKdf.DeriveKek(input.Password, slot.KdfSalt, slot.Kdf.ToParameters());
        var aad = VmkUnwrapAad.Build(3, slot.ContainerId, slot.SlotId, slot.Generation);
        var blob = new AstraCiphertext(slot.WrapNonce, slot.WrapTag, slot.WrappedVmk);
        var vmk = AstraAead.Decrypt(AstraCipherSuite.XChaCha20Poly1305, kek, blob, aad);
        AstraKdf.Zero(kek);
        Assert.Equal(32, vmk.Length);
        Assert.Equal(Hex("0101010101010101010101010101010101010101010101010101010101010101"), vmk);
    }

    [Fact]
    public void Golden_ActivationAead_FromHeader_Pass()
    {
        var input = LoadInput();
        var locator = VaultLocator.Parse(ReadVector("locator.bin"));
        var copy = VaultHeaderCopy.Parse(ReadVector("header-copy-0.bin"), locator.HeaderCopySize);
        var slot = copy.PasswordSlots[0];
        var kek = AstraKdf.DeriveKek(input.Password, slot.KdfSalt, slot.Kdf.ToParameters());
        var aad = VmkUnwrapAad.Build(3, slot.ContainerId, slot.SlotId, slot.Generation);
        var blob = new AstraCiphertext(slot.WrapNonce, slot.WrapTag, slot.WrappedVmk);
        var vmk = AstraAead.Decrypt(AstraCipherSuite.XChaCha20Poly1305, kek, blob, aad);
        AstraKdf.Zero(kek);
        var aadAct = ActivationPayloadAad.Build(
            3,
            copy.ContainerId,
            copy.VaultId,
            copy.CopyIndex,
            copy.Generation,
            copy.CipherSuiteId,
            copy.MetadataRootCiphertextDigest,
            copy.ActivationTarget);
        var plain = HeaderActivationAead.AuthenticateAndDecrypt(copy, vmk);
        AstraKdf.Zero(vmk);
        Assert.Equal(HeaderActivationPayload.PlaintextSize, plain.Length);
    }

    [Fact]
    public void Golden_ValidActivation_AeadAuthenticationPass()
    {
        var input = LoadInput();
        var locator = ReadVector("locator.bin");
        var header0 = ReadVector("header-copy-0.bin");
        var meta = ReadMetadataEnvelope();
        var result = ReadOnlyUnlockValidator.Validate(
            locator,
            [(0, header0), (1, ReadVector("header-copy-1.bin")), (2, ReadVector("header-copy-2.bin"))],
            meta,
            input.Password);
        Assert.Equal(VaultSecurityState.ReadOnlyUnlocked, result.State);
        Assert.Equal(42ul, result.SelectedHeader.Generation);
        Assert.NotNull(result.MetadataValidation);
        Assert.True(result.MetadataValidation!.Authenticated);
    }

    [Fact]
    public void Golden_PasswordSlotAad_StableAgainstProductionBuilder()
    {
        var expected = ReadVector("password-slot-aad.bin");
        var containerId = new Guid("a3f1c2e4-5b6d-4789-a012-3456789abcde");
        var built = VmkUnwrapAad.Build(3, containerId, slotId: 1, generation: 42);
        Assert.Equal(expected, built);
    }

    [Fact]
    public void Golden_VmkUnwrapAad_Stable()
    {
        Assert.Equal(ReadVector("vmk-unwrap-aad.bin"), ReadVector("password-slot-aad.bin"));
    }

    [Fact]
    public void Golden_ActivationPayloadAad_Stable()
    {
        var containerId = new Guid("a3f1c2e4-5b6d-4789-a012-3456789abcde");
        using var expectedDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(OutputDir, "metadata-root-expected-result.json")));
        var digest = Hex(expectedDoc.RootElement.GetProperty("metadata_ciphertext_digest_hex").GetString()!);
        var built = ActivationPayloadAad.Build(3, containerId, containerId, 0, 42, 1, digest, 1);
        Assert.Equal(ReadVector("activation-payload-aad.bin"), built);
    }

    [Fact]
    public void Golden_MetadataRootDescriptor_StableParse()
    {
        var parsed = MetadataRootDescriptor.Parse(ReadVector("metadata-root-descriptor.bin"));
        Assert.Equal(42ul, parsed.Generation);
        Assert.Equal(41ul, parsed.ParentGeneration);
    }

    [Fact]
    public void Golden_ActivationTagTamper_Fail()
    {
        var input = LoadInput();
        var header = ReadVector("header-copy-0.bin").ToArray();
        header[204] ^= 0x55;
        Assert.Throws<UnlockValidationException>(() =>
            ReadOnlyUnlockValidator.Validate(
                ReadVector("locator.bin"),
                [(0, header)],
                ReadMetadataEnvelope(),
                input.Password));
    }

    [Fact]
    public void Golden_AadTamper_Fail()
    {
        var input = LoadInput();
        var header = ReadVector("header-copy-0.bin").ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), 99);
        Assert.Throws<UnlockValidationException>(() =>
            ReadOnlyUnlockValidator.Validate(
                ReadVector("locator.bin"),
                [(0, header)],
                ReadMetadataEnvelope(),
                input.Password));
    }

    [Fact]
    public void Golden_WrongPassword_And_MalformedSlot_SamePublicMessage()
    {
        var input = LoadInput();
        var locator = ReadVector("locator.bin");
        var meta = ReadMetadataEnvelope();
        var header = ReadVector("header-copy-0.bin").ToArray();
        header[VaultHeaderCopy.FixedRegionSize] = 0xEE;

        string? wrong = null;
        string? malformed = null;
        try
        {
            ReadOnlyUnlockValidator.Validate(locator, [(0, ReadVector("header-copy-0.bin"))], meta, "wrong");
        }
        catch (UnlockValidationException ex)
        {
            wrong = ex.Message;
        }

        try
        {
            ReadOnlyUnlockValidator.Validate(locator, [(0, header)], meta, input.Password);
        }
        catch (UnlockValidationException ex)
        {
            malformed = ex.Message;
        }

        Assert.Equal(UnlockValidationException.PublicMessage, wrong);
        Assert.Equal(wrong, malformed);
        Assert.DoesNotContain(input.Password, wrong ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Golden_OutputHashes_MatchManifest()
    {
        var manifestPath = Path.Combine(OutputDir, "manifest.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var expected = doc.RootElement.GetProperty("output_hashes");
        foreach (var prop in expected.EnumerateObject())
        {
            if (string.Equals(prop.Name, "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var actual = Convert.ToHexString(SHA256.HashData(ReadVector(prop.Name))).ToLowerInvariant();
            Assert.Equal(prop.Value.GetString(), actual);
        }
    }

    [Fact]
    public void Golden_SameInput_ProducesByteForByteSameOutput()
    {
        var repoRoot = FindRepoRoot();
        var genProj = Path.Combine(repoRoot, "tools", "astra-vault-vector-gen", "AstraVaultVectorGen.csproj");
        var tempRoot = Path.Combine(Path.GetTempPath(), "av3-golden-" + Guid.NewGuid().ToString("N"));
        var tempRoot2 = Path.Combine(Path.GetTempPath(), "av3-golden-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(tempRoot2);
            File.Copy(Path.Combine(VectorRoot, "reference-input.json"), Path.Combine(tempRoot, "reference-input.json"));
            File.Copy(Path.Combine(VectorRoot, "reference-input.json"), Path.Combine(tempRoot2, "reference-input.json"));

            RunGenerator(genProj, tempRoot);
            RunGenerator(genProj, tempRoot2);

            var files = new[]
            {
                "locator.bin", "header-copy-0.bin", "password-slot-aad.bin", "activation-payload-aad.bin"
            };
            foreach (var file in files)
            {
                var a = File.ReadAllBytes(Path.Combine(tempRoot, "reference-output", file));
                var b = File.ReadAllBytes(Path.Combine(tempRoot2, "reference-output", file));
                Assert.Equal(a, b);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, true);
                Directory.Delete(tempRoot2, true);
            }
            catch
            {
                // best-effort temp cleanup
            }
        }
    }

    private static void RunGenerator(string genProj, string av3Root)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{genProj}\" -c Release -- \"{av3Root}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start generator.");
        proc.WaitForExit(120_000);
        Assert.Equal(0, proc.ExitCode);
    }

    private static GoldenInput LoadInput()
    {
        var json = File.ReadAllText(Path.Combine(VectorRoot, "reference-input.json"));
        return JsonSerializer.Deserialize<GoldenInput>(json) ?? throw new InvalidOperationException();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
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

    private static byte[] Hex(string h)
    {
        var bytes = new byte[h.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private sealed class GoldenInput
    {
        [System.Text.Json.Serialization.JsonPropertyName("password_test_only")]
        public string Password { get; init; } = "";
    }
}