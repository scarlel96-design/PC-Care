using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Experimental;

/// <summary>Real activation + metadata-root AEAD for E-2 harness commits (test-only).</summary>
public static class Av3HarnessCommitCrypto
{
    public const ushort HarnessCipherSuite = (ushort)AstraCipherSuite.XChaCha20Poly1305;

    public static byte[] EncryptHarnessObject(ReadOnlySpan<byte> vmk, ulong targetGeneration, ReadOnlySpan<byte> plaintext)
    {
        var genBytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(genBytes, targetGeneration);
        var key = AstraKdf.DeriveDomainKey(vmk, genBytes, Av3HarnessCommitContext.ObjectKeyDomain);
        try
        {
            var nonce = SHA256.HashData(genBytes).AsSpan(0, AstraAead.ChaChaNonceSize).ToArray();
            var aad = SHA256.HashData("astra-harness-object-aad"u8);
            return PackBlob(AstraAead.Encrypt(AstraCipherSuite.XChaCha20Poly1305, key, plaintext, aad));
        }
        finally
        {
            AstraKdf.Zero(key);
        }
    }

    public static Av3HarnessMetadataArtifacts BuildMetadataRootArtifacts(
        ReadOnlySpan<byte> vmk,
        Av3WritePlan plan) =>
        BuildMetadataRootArtifacts(vmk, plan, HarnessCipherSuite);

    public static Av3HarnessMetadataArtifacts BuildMetadataRootArtifacts(
        ReadOnlySpan<byte> vmk,
        Av3WritePlan plan,
        ushort cipherSuite)
    {
        var graphRoot = DeriveRootSeed(plan.ContainerId, plan.TargetGeneration, "graph");
        var allocationRoot = DeriveRootSeed(plan.ContainerId, plan.TargetGeneration, "alloc");
        var indexRoot = DeriveRootSeed(plan.ContainerId, plan.TargetGeneration, "index");
        var journalHead = DeriveRootSeed(plan.ContainerId, plan.TargetGeneration, "journal");
        var recoveryRoot = DeriveRootSeed(plan.ContainerId, plan.TargetGeneration, "recovery");

        var metadataPlain = MetadataRootPlaintext.BuildCanonical(
            cipherSuite,
            plan.TargetGeneration,
            plan.PreviousGeneration,
            graphRoot,
            allocationRoot,
            indexRoot,
            journalHead,
            recoveryRoot);

        var metaPlainCommit = MetadataRootPlaintext.ComputeRootPlaintextCommitment(metadataPlain);
        var activationPlain = HeaderActivationPayload.BuildPlaintext(
            plan.TargetGeneration,
            metaPlainCommit,
            plan.TargetGeneration,
            plan.PreviousGeneration);
        var activationDigest = HeaderActivationPayload.DigestFromPlaintext(activationPlain);

        var metaDigest = new byte[32];
        var metaNonce = Av3AeadNoncePolicy.FixtureNonce(
            cipherSuite,
            System.Text.Encoding.UTF8.GetBytes($"av3-harness-meta:{plan.ContainerId:N}:{plan.TargetGeneration}"));
        var metaCipher = new byte[MetadataRootPlaintext.PlaintextSize];
        var metaTag = new byte[AstraAead.TagSize];
        var converged = false;
        for (var round = 0; round < 32; round++)
        {
            var metaAad = MetadataRootAad.Build(
                AstraFormatConstants.MajorVersion,
                cipherSuite,
                plan.ContainerId,
                plan.ContainerId,
                plan.TargetGeneration,
                plan.TargetGeneration,
                metaDigest,
                activationDigest,
                MetadataRootReadOnlyReader.DefaultLogicalId,
                MetadataRootPlaintext.PlaintextSize);

            var metadataKey = MetadataRootAead.DeriveMetadataKey(vmk);
            var metaCt = EncryptHarnessWithFixtureNonce(cipherSuite, metadataKey, metadataPlain, metaAad, metaNonce);
            metaCt.Cipher.CopyTo(metaCipher);
            metaCt.Tag.CopyTo(metaTag);
            AstraKdf.Zero(metadataKey);
            var nextDigest = SHA256.HashData(metaCipher);
            if (round > 0 && nextDigest.AsSpan().SequenceEqual(metaDigest))
            {
                converged = true;
                break;
            }

            metaDigest = nextDigest;
        }

        if (!converged)
        {
            throw new CryptographicException("av3_harness_metadata_digest_not_converged");
        }

        var envelope = new byte[MetadataRootDescriptor.DescriptorSize + metaCipher.Length];
        MetadataRootDescriptor.RootMagic.CopyTo(envelope);
        BinaryPrimitives.WriteUInt16LittleEndian(envelope.AsSpan(4), MetadataRootDescriptor.RootStructVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(envelope.AsSpan(6), cipherSuite);
        var (metaNonceOffset, _, metaTagOffset, ctLenOffset) = Av3AeadOnDiskLayout.MetadataRootNonceTagOffsets(cipherSuite);
        BinaryPrimitives.WriteUInt64LittleEndian(envelope.AsSpan(8), plan.TargetGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(envelope.AsSpan(16), plan.PreviousGeneration);
        plan.ContainerId.TryWriteBytes(envelope.AsSpan(24, 16));
        metaDigest.CopyTo(envelope.AsSpan(72, 32));
        metaNonce.CopyTo(envelope.AsSpan(metaNonceOffset, metaNonce.Length));
        metaTag.CopyTo(envelope.AsSpan(metaTagOffset, metaTag.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(envelope.AsSpan(ctLenOffset), (uint)metaCipher.Length);
        metaCipher.CopyTo(envelope.AsSpan(MetadataRootDescriptor.DescriptorSize));

        return new Av3HarnessMetadataArtifacts
        {
            Envelope = envelope,
            CiphertextDigest = metaDigest,
            PlaintextCommitment = metaPlainCommit,
            ActivationPayloadDigest = activationDigest
        };
    }

    public static byte[] BuildActivationHeaderCopy(
        ReadOnlySpan<byte> vmk,
        Av3WritePlan plan,
        ReadOnlySpan<byte> metadataRootCiphertextDigest,
        ReadOnlySpan<byte> metadataRootPlaintextCommitment,
        byte copyIndex = 0,
        ushort cipherSuite = HarnessCipherSuite)
    {
        var activationPlain = HeaderActivationPayload.BuildPlaintext(
            plan.TargetGeneration,
            metadataRootPlaintextCommitment,
            plan.TargetGeneration,
            plan.PreviousGeneration);
        var activationDigest = HeaderActivationPayload.DigestFromPlaintext(activationPlain);

        var activationKey = HeaderActivationAead.DeriveActivationKey(vmk);
        byte[] header;
        try
        {
            var activationAad = ActivationPayloadAad.Build(
                AstraFormatConstants.MajorVersion,
                plan.ContainerId,
                plan.ContainerId,
                copyIndex,
                plan.TargetGeneration,
                cipherSuite,
                metadataRootCiphertextDigest,
                ActivationPayloadAad.TargetMetadataRoot);
            var activationCt = AstraAead.Encrypt(
                Av3AeadDispatch.ToCipherSuite(cipherSuite),
                activationKey,
                activationPlain,
                activationAad);

            header = new byte[VaultHeaderCopy.ExpectedCopySize(1)];
            VaultHeaderCopy.HeaderMagic.CopyTo(header);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), VaultHeaderCopy.HeaderStructVersion);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), plan.TargetGeneration);
            plan.ContainerId.TryWriteBytes(header.AsSpan(16, 16));
            header[32] = copyIndex;
            header[33] = 1;
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34), cipherSuite);
            var (actNonceOffset, _, actTagOffset) = Av3AeadOnDiskLayout.HeaderActivationOffsets(cipherSuite);
            var actCipherOffset = actTagOffset + AstraAead.TagSize;
            Argon2idKdfDescriptor.FromParameters(AstraArgon2Parameters.LowMemory)
                .Write(header.AsSpan(36, Argon2idKdfDescriptor.Size));
            activationDigest.CopyTo(header.AsSpan(60, 32));
            metadataRootPlaintextCommitment.CopyTo(header.AsSpan(92, 32));
            metadataRootCiphertextDigest.CopyTo(header.AsSpan(124, 32));
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(156), plan.TargetGeneration);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(164), plan.PreviousGeneration);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(172), ActivationPayloadAad.TargetMetadataRoot);
            plan.ContainerId.TryWriteBytes(header.AsSpan(176, 16));
            activationCt.Nonce.CopyTo(header.AsSpan(actNonceOffset));
            activationCt.Tag.CopyTo(header.AsSpan(actTagOffset));
            activationCt.Cipher.CopyTo(header.AsSpan(actCipherOffset));

            var salt = RandomNumberGenerator.GetBytes(32);
            var kdfDesc = Argon2idKdfDescriptor.FromParameters(AstraArgon2Parameters.LowMemory);
            var kek = AstraKdf.DeriveKek("av3-harness-placeholder", salt, AstraArgon2Parameters.LowMemory);
            try
            {
                var wrapAad = VmkUnwrapAad.Build(
                    AstraFormatConstants.MajorVersion,
                    plan.ContainerId,
                    slotId: 1,
                    plan.TargetGeneration);
                var wrapSuiteId = cipherSuite == Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24
                    ? Av3AeadAlgorithmId.ChaCha12Transitional
                    : cipherSuite;
                var wrapped = AstraAead.Encrypt(Av3AeadDispatch.ToCipherSuite(wrapSuiteId), kek, vmk, wrapAad);
                var slot = new PasswordSlotEnvelope
                {
                    SlotId = 1,
                    CipherSuiteId = wrapSuiteId,
                    KdfSuiteId = AstraSuiteIds.KdfArgon2id,
                    Generation = plan.TargetGeneration,
                    ContainerId = plan.ContainerId,
                    KdfSalt = salt,
                    Kdf = kdfDesc,
                    WrapNonce = wrapped.Nonce,
                    WrapTag = wrapped.Tag,
                    WrappedVmk = wrapped.Cipher
                };
                slot.Write(header.AsSpan(VaultHeaderCopy.FixedRegionSize, PasswordSlotEnvelope.Size));
            }
            finally
            {
                AstraKdf.Zero(kek);
            }
        }
        finally
        {
            AstraKdf.Zero(activationKey);
        }

        return header;
    }

    public static bool TryAuthenticatePostCommit(
        ReadOnlySpan<byte> headerCopyBytes,
        ReadOnlySpan<byte> metadataRootEnvelope,
        ReadOnlySpan<byte> vmk) =>
        Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(headerCopyBytes, metadataRootEnvelope, vmk).Success;

    private static byte[] DeriveRootSeed(Guid containerId, ulong generation, string label)
    {
        var labelBytes = System.Text.Encoding.UTF8.GetBytes(label);
        var gen = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(gen, generation);
        return SHA256.HashData([.. containerId.ToByteArray(), .. gen, .. labelBytes]);
    }

    private static AstraCiphertext EncryptHarnessWithFixtureNonce(
        ushort cipherSuite,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> nonce)
    {
        var cipher = new byte[plaintext.Length];
        var tag = new byte[AstraAead.TagSize];
        if (cipherSuite == Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24)
        {
            XChaCha20Poly1305.Encrypt(key, nonce, plaintext, cipher, tag, aad);
        }
        else if (cipherSuite == Av3AeadAlgorithmId.Aes256Gcm)
        {
            using var aes = new AesGcm(key, AstraAead.TagSize);
            aes.Encrypt(nonce, plaintext, cipher, tag, aad);
        }
        else
        {
            using var chacha = new ChaCha20Poly1305(key);
            chacha.Encrypt(nonce, plaintext, cipher, tag, aad);
        }

        return new AstraCiphertext(nonce.ToArray(), tag, cipher);
    }

    private static byte[] PackBlob(AstraCiphertext blob)
    {
        var packed = new byte[blob.Nonce.Length + blob.Tag.Length + blob.Cipher.Length];
        blob.Nonce.CopyTo(packed, 0);
        blob.Tag.CopyTo(packed, blob.Nonce.Length);
        blob.Cipher.CopyTo(packed, blob.Nonce.Length + blob.Tag.Length);
        return packed;
    }
}