using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AstraVaultVectorGen;

internal sealed class GoldenVectorWriter
{
    private const string GeneratorVersion = "1.1.0-phase-d";
    private readonly ReferenceInputDocument _input;
    private readonly string _outputDir;

    public GoldenVectorWriter(ReferenceInputDocument input, string outputDir)
    {
        _input = input;
        _outputDir = outputDir;
        Directory.CreateDirectory(_outputDir);
    }

    public void GenerateAll()
    {
        var containerId = Guid.Parse(_input.ContainerId);
        var vaultId = Guid.Parse(_input.VaultId);
        var vmk = StandaloneFixtureCrypto.ParseHex(_input.VmkHex);
        var salt = StandaloneFixtureCrypto.ParseHex(_input.KdfSaltHex);
        var wrapNonce = StandaloneFixtureCrypto.ParseHex(_input.VmkWrapNonceHex);
        var activationNonce = StandaloneFixtureCrypto.ParseHex(_input.ActivationNonceHex);
        var parentHash = StandaloneFixtureCrypto.ParseHex(_input.MetadataRootParentHashHex);
        var metaNonce = StandaloneFixtureCrypto.ParseHex(_input.MetadataRootNonceHex);
        var graphRoot = StandaloneFixtureCrypto.ParseHex(_input.MetadataGraphRootDigestHex);
        var allocationRoot = StandaloneFixtureCrypto.ParseHex(_input.MetadataAllocationRootDigestHex);
        var indexRoot = StandaloneFixtureCrypto.ParseHex(_input.MetadataIndexRootDigestHex);
        var journalHead = StandaloneFixtureCrypto.ParseHex(_input.MetadataJournalHeadCommitmentHex);
        var recoveryRoot = StandaloneFixtureCrypto.ParseHex(_input.MetadataRecoveryRootDigestHex);

        var metadataPlain = StandaloneFixtureCrypto.BuildMetadataRootPlaintext(
            _input.CipherSuiteId,
            _input.MetadataGeneration,
            _input.ParentMetadataGeneration,
            graphRoot,
            allocationRoot,
            indexRoot,
            journalHead,
            recoveryRoot);
        var metaPlainCommit = metadataPlain.AsSpan(StandaloneFixtureCrypto.MetadataCommitmentOffset, 32).ToArray();

        var vmkAad = StandaloneFixtureCrypto.BuildVmkUnwrapAad(
            _input.FormatVersion, containerId, _input.KeySlotId, _input.Generation);
        var kek = StandaloneFixtureCrypto.DeriveKek(_input.PasswordTestOnly, salt, _input.Argon2Id);
        var wrappedCipher = new byte[StandaloneFixtureCrypto.KeySize];
        var wrappedTag = new byte[StandaloneFixtureCrypto.TagSize];
        StandaloneFixtureCrypto.ChaChaEncrypt(kek, wrapNonce, vmk, vmkAad, wrappedCipher, wrappedTag);
        CryptographicOperations.ZeroMemory(kek);

        var activationPlain = StandaloneFixtureCrypto.BuildActivationPlain(
            _input.Generation, metaPlainCommit, _input.MetadataGeneration, _input.ParentMetadataGeneration);
        var activationDigest = SHA256.HashData(activationPlain);

        var metaDigest = new byte[32];
        var metaCipher = new byte[StandaloneFixtureCrypto.MetadataPlainSize];
        var metaTag = new byte[StandaloneFixtureCrypto.TagSize];
        byte[] metaAad = [];
        for (var round = 0; round < 4; round++)
        {
            metaAad = StandaloneFixtureCrypto.BuildMetadataRootAad(
                _input.FormatVersion,
                _input.CipherSuiteId,
                containerId,
                vaultId,
                _input.Generation,
                _input.MetadataGeneration,
                metaDigest,
                activationDigest,
                _input.MetadataRootLogicalId,
                _input.MetadataRootCiphertextLength);
            var metadataKey = StandaloneFixtureCrypto.HkdfDomain(vmk, StandaloneFixtureCrypto.MetadataRootAadDomain);
            StandaloneFixtureCrypto.ChaChaEncrypt(metadataKey, metaNonce, metadataPlain, metaAad, metaCipher, metaTag);
            CryptographicOperations.ZeroMemory(metadataKey);
            var nextDigest = SHA256.HashData(metaCipher);
            if (nextDigest.AsSpan().SequenceEqual(metaDigest))
            {
                break;
            }

            metaDigest = nextDigest;
        }

        var activationKey = StandaloneFixtureCrypto.HkdfDomain(vmk, "astra-header-activation");
        var activationAad0 = StandaloneFixtureCrypto.BuildActivationAad(
            _input.FormatVersion, containerId, vaultId, 0, _input.Generation,
            _input.CipherSuiteId, metaDigest, _input.ActivationTarget);
        var (actCipher0, actTag0) = EncryptActivation(activationKey, activationNonce, activationPlain, activationAad0);
        var activationAad1 = StandaloneFixtureCrypto.BuildActivationAad(
            _input.FormatVersion, containerId, vaultId, 1, _input.Generation,
            _input.CipherSuiteId, metaDigest, _input.ActivationTarget);
        var (actCipher1, actTag1) = EncryptActivation(activationKey, activationNonce, activationPlain, activationAad1);
        var activationAad2 = StandaloneFixtureCrypto.BuildActivationAad(
            _input.FormatVersion, containerId, vaultId, 2, _input.Generation,
            _input.CipherSuiteId, metaDigest, _input.ActivationTarget);
        var (actCipher2, actTag2) = EncryptActivation(activationKey, activationNonce, activationPlain, activationAad2);
        CryptographicOperations.ZeroMemory(activationKey);

        var headerSize = StandaloneFixtureCrypto.HeaderFixed + StandaloneFixtureCrypto.SlotSize;
        var locator = BuildLocator(containerId, headerSize);
        var header0 = BuildHeaderCopy(
            containerId, vaultId, 0, salt, wrapNonce, wrappedCipher, wrappedTag,
            activationNonce, actTag0, actCipher0, activationDigest, metaPlainCommit, metaDigest);
        var header1 = BuildHeaderCopy(
            containerId, vaultId, 1, salt, wrapNonce, wrappedCipher, wrappedTag,
            activationNonce, actTag1, actCipher1, activationDigest, metaPlainCommit, metaDigest);
        var header2 = BuildHeaderCopy(
            containerId, vaultId, 2, salt, wrapNonce, wrappedCipher, wrappedTag,
            activationNonce, actTag2, actCipher2, activationDigest, metaPlainCommit, metaDigest);

        var metadata = BuildMetadataRoot(containerId, metaDigest, parentHash, metaNonce, metaTag);
        var commitmentPreimage = metadataPlain.AsSpan(0, StandaloneFixtureCrypto.MetadataCommitmentOffset).ToArray();

        WriteFile("locator.bin", locator);
        WriteFile("header-copy-0.bin", header0);
        WriteFile("header-copy-1.bin", header1);
        WriteFile("header-copy-2.bin", header2);
        WriteFile("password-slot-aad.bin", vmkAad);
        WriteFile("password-slot-ciphertext.bin", wrappedCipher);
        WriteFile("password-slot-tag.bin", wrappedTag);
        WriteFile("activation-payload-aad.bin", activationAad0);
        WriteFile("activation-payload-plaintext.bin", activationPlain);
        WriteFile("activation-payload-ciphertext.bin", actCipher0);
        WriteFile("activation-payload-tag.bin", actTag0);
        WriteFile("metadata-root-descriptor.bin", metadata);
        WriteFile("metadata-root-aad.bin", metaAad);
        WriteFile("metadata-root-plaintext.bin", metadataPlain);
        WriteFile("metadata-root-ciphertext.bin", metaCipher);
        WriteFile("metadata-root-tag.bin", metaTag);
        WriteFile("metadata-root-commitment-preimage.bin", commitmentPreimage);
        WriteFile("metadata-root-commitment.bin", metaPlainCommit);
        var expectedMetadata = new
        {
            authenticated = true,
            generation = _input.MetadataGeneration,
            parent_generation = _input.ParentMetadataGeneration,
            plaintext_size = StandaloneFixtureCrypto.MetadataPlainSize,
            ciphertext_length = _input.MetadataRootCiphertextLength,
            metadata_root_logical_id = _input.MetadataRootLogicalId,
            root_plaintext_commitment_hex = Convert.ToHexString(metaPlainCommit).ToLowerInvariant(),
            metadata_ciphertext_digest_hex = Convert.ToHexString(metaDigest).ToLowerInvariant()
        };
        WriteJson("metadata-root-expected-result.json", expectedMetadata);
        WriteFile("vmk-unwrap-aad.bin", vmkAad);

        var expectedErrors = new
        {
            uniform_public_message = _input.ExpectedPublicErrorMessage,
            error_type_names = _input.ExpectedPublicErrorNames
        };
        WriteJson("expected-errors.json", expectedErrors);

        var provenance = new
        {
            classification = "TEST_ONLY",
            no_user_data = true,
            no_network = true,
            generator = "astra-vault-vector-gen",
            generator_version = GeneratorVersion
        };
        WriteJson("provenance.json", provenance);

        WriteManifest();
        CryptographicOperations.ZeroMemory(vmk);
    }

    private static (byte[] Cipher, byte[] Tag) EncryptActivation(
        byte[] activationKey,
        byte[] nonce,
        byte[] plain,
        byte[] aad)
    {
        var cipher = new byte[StandaloneFixtureCrypto.ActivationPlainSize];
        var tag = new byte[StandaloneFixtureCrypto.TagSize];
        StandaloneFixtureCrypto.ChaChaEncrypt(activationKey, nonce, plain, aad, cipher, tag);
        return (cipher, tag);
    }

    private byte[] BuildLocator(Guid containerId, int headerSize)
    {
        var buf = new byte[StandaloneFixtureCrypto.LocatorSize];
        "AVLT"u8.CopyTo(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), _input.FormatVersion);
        containerId.TryWriteBytes(buf.AsSpan(8, 16));
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(24), _input.CipherSuiteId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(26), _input.KdfSuiteId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(28), 512);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(36), (ulong)(512 + headerSize));
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(44), (ulong)(512 + headerSize * 2));
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(52), (uint)headerSize);
        return buf;
    }

    private byte[] BuildHeaderCopy(
        Guid containerId,
        Guid vaultId,
        byte copyIndex,
        byte[] salt,
        byte[] wrapNonce,
        byte[] wrapCipher,
        byte[] wrapTag,
        byte[] actNonce,
        byte[] actTag,
        byte[] actCipher,
        byte[] actDigest,
        byte[] metaPlain,
        byte[] metaDigest)
    {
        var buf = new byte[StandaloneFixtureCrypto.HeaderFixed + StandaloneFixtureCrypto.SlotSize];
        "VHDR"u8.CopyTo(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(8), _input.Generation);
        containerId.TryWriteBytes(buf.AsSpan(16, 16));
        buf[32] = copyIndex;
        buf[33] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(34), _input.CipherSuiteId);
        WriteKdfDescriptor(buf.AsSpan(36, 24));
        actDigest.CopyTo(buf.AsSpan(60, 32));
        metaPlain.CopyTo(buf.AsSpan(92, 32));
        metaDigest.CopyTo(buf.AsSpan(124, 32));
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(156), _input.MetadataGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(164), _input.ParentMetadataGeneration);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(172), _input.ActivationTarget);
        vaultId.TryWriteBytes(buf.AsSpan(176, 16));
        actNonce.CopyTo(buf.AsSpan(192, 12));
        actTag.CopyTo(buf.AsSpan(204, 16));
        actCipher.CopyTo(buf.AsSpan(220, 64));

        var slot = buf.AsSpan(StandaloneFixtureCrypto.HeaderFixed, StandaloneFixtureCrypto.SlotSize);
        "PSLT"u8.CopyTo(slot);
        BinaryPrimitives.WriteUInt16LittleEndian(slot.Slice(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(slot.Slice(6), _input.KeySlotId);
        BinaryPrimitives.WriteUInt16LittleEndian(slot.Slice(8), _input.CipherSuiteId);
        BinaryPrimitives.WriteUInt16LittleEndian(slot.Slice(10), _input.KdfSuiteId);
        BinaryPrimitives.WriteUInt64LittleEndian(slot.Slice(12), _input.Generation);
        containerId.TryWriteBytes(slot.Slice(20, 16));
        salt.CopyTo(slot.Slice(36, 32));
        WriteKdfDescriptor(slot.Slice(68, 24));
        wrapNonce.CopyTo(slot.Slice(92, 12));
        wrapTag.CopyTo(slot.Slice(104, 16));
        wrapCipher.CopyTo(slot.Slice(120, 32));
        return buf;
    }

    private void WriteKdfDescriptor(Span<byte> dest)
    {
        dest.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(dest, _input.KdfSuiteId);
        BinaryPrimitives.WriteUInt16LittleEndian(dest.Slice(2), _input.Argon2Id.ProfileId);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.Slice(4), (uint)_input.Argon2Id.MemoryKiB);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.Slice(8), (uint)_input.Argon2Id.Iterations);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.Slice(12), (uint)_input.Argon2Id.Parallelism);
    }

    private byte[] BuildMetadataRoot(Guid containerId, byte[] metaDigest, byte[] parentHash, byte[] nonce, byte[] tag)
    {
        var buf = new byte[StandaloneFixtureCrypto.MetadataDescriptorSize];
        "MROT"u8.CopyTo(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6), _input.CipherSuiteId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(8), _input.MetadataGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(16), _input.ParentMetadataGeneration);
        containerId.TryWriteBytes(buf.AsSpan(24, 16));
        parentHash.CopyTo(buf.AsSpan(40, 32));
        metaDigest.CopyTo(buf.AsSpan(72, 32));
        nonce.CopyTo(buf.AsSpan(104, nonce.Length));
        tag.CopyTo(buf.AsSpan(116, tag.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(132), _input.MetadataRootCiphertextLength);
        return buf;
    }

    private void WriteFile(string name, byte[] bytes) => File.WriteAllBytes(Path.Combine(_outputDir, name), bytes);

    private void WriteJson(string name, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_outputDir, name), json, Encoding.UTF8);
    }

    private void WriteManifest()
    {
        var files = Directory.GetFiles(_outputDir)
            .Where(f => !string.Equals(Path.GetFileName(f), "manifest.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            hashes[Path.GetFileName(file)] = hash;
        }

        var inputPath = Path.Combine(Path.GetDirectoryName(_outputDir)!, "reference-input.json");
        var inputHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(inputPath))).ToLowerInvariant();

        var manifest = new
        {
            generator_version = GeneratorVersion,
            generated_at = "2026-07-04T00:00:00Z",
            source_commit_or_worktree_marker = "phase-d-golden-lock",
            input_hash = inputHash,
            output_hashes = hashes,
            crypto_dependency_versions = new { argon2 = "1.3.1", dotnet = "net10.0" },
            vector_status = "LOCKED",
            pending_items = Array.Empty<string>(),
            reviewer_notes_required = true
        };
        WriteJson("manifest.json", manifest);
    }
}