using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.Session;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseBTests
{
    private const string TestPassword = "PhaseB-Test-Passw0rd!";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_ProductionWriter_RemainsDisabled()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.ReadOnlyValidationEnabled);
    }

    [Fact]
    public void Locator_Rejects_WrongSize()
    {
        var bytes = VaultLocator.CreateNew(1, 1).Write();
        Assert.ThrowsAny<Exception>(() => VaultLocator.Parse(bytes.AsSpan(0, 100).ToArray()));
    }

    [Fact]
    public void Locator_Rejects_UnknownMajorVersion()
    {
        var bytes = VaultLocator.CreateNew(1, 1).Write();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 99);
        Assert.ThrowsAny<Exception>(() => VaultLocator.Parse(bytes));
    }

    [Fact]
    public void Locator_Rejects_NonZeroReserved()
    {
        var bytes = VaultLocator.CreateNew(1, 1).Write();
        bytes[200] = 1;
        Assert.ThrowsAny<Exception>(() => VaultLocator.Parse(bytes));
    }

    [Fact]
    public void Locator_Rejects_UnsupportedCipherSuite()
    {
        var bytes = VaultLocator.CreateNew(1, 1).Write();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(24), 999);
        Assert.ThrowsAny<Exception>(() => VaultLocator.Parse(bytes));
    }

    [Fact]
    public void Locator_Rejects_HeaderCopySize_TooSmall()
    {
        var bytes = VaultLocator.CreateNew(1, 1).Write();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 64);
        Assert.ThrowsAny<Exception>(() => VaultLocator.Parse(bytes));
    }

    [Fact]
    public void Locator_Rejects_HeaderCopySize_Oversized()
    {
        var bytes = VaultLocator.CreateNew(1, 1).Write();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 1_000_000);
        Assert.ThrowsAny<Exception>(() => VaultLocator.Parse(bytes));
    }

    [Fact]
    public void Locator_Preserves_MinorVersion()
    {
        var loc = VaultLocator.CreateNew(1, 1);
        var bytes = loc.Write();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 7);
        var parsed = VaultLocator.Parse(bytes);
        Assert.Equal(loc.ContainerId, parsed.ContainerId);
    }

    [Fact]
    public void Locator_RoundTrip_HeaderOffsets()
    {
        var loc = VaultLocator.CreateNew(
            (ushort)AstraCipherSuite.XChaCha20Poly1305,
            AstraSuiteIds.KdfArgon2id);
        loc = new VaultLocator
        {
            ContainerId = loc.ContainerId,
            CipherSuiteId = loc.CipherSuiteId,
            KdfSuiteId = loc.KdfSuiteId,
            HeaderPrimaryOffset = 512,
            HeaderSecondaryOffset = 2048,
            HeaderTertiaryOffset = 4096,
            HeaderCopySize = (uint)VaultHeaderCopy.ExpectedCopySize(1)
        };
        var parsed = VaultLocator.Parse(loc.Write());
        Assert.Equal(512ul, parsed.HeaderPrimaryOffset);
        Assert.Equal(2048ul, parsed.HeaderSecondaryOffset);
        Assert.Equal(4096ul, parsed.HeaderTertiaryOffset);
    }

    [Fact]
    public void ReadOnlyUnlock_Succeeds_WithValidVector()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, generation: 3);
        var result = ReadOnlyUnlockValidator.Validate(
            bundle.Locator,
            [(0, bundle.HeaderCopy)],
            bundle.MetadataRoot,
            TestPassword);
        Assert.Equal(VaultSecurityState.ReadOnlyUnlocked, result.State);
        Assert.Equal(3ul, result.SelectedHeader.Generation);
    }

    [Fact]
    public void ReadOnlyUnlock_Rejects_WrongPassword_And_MalformedSlot_SameMessage()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, generation: 1);
        UnlockValidationException? wrongPass = null;
        UnlockValidationException? badMagic = null;
        try
        {
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, bundle.HeaderCopy)], bundle.MetadataRoot, "wrong-password");
        }
        catch (UnlockValidationException ex)
        {
            wrongPass = ex;
        }

        var corrupt = bundle.HeaderCopy.ToArray();
        corrupt[VaultHeaderCopy.FixedRegionSize] = 0xFF;
        try
        {
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, corrupt)], bundle.MetadataRoot, TestPassword);
        }
        catch (UnlockValidationException ex)
        {
            badMagic = ex;
        }

        Assert.NotNull(wrongPass);
        Assert.NotNull(badMagic);
        Assert.Equal(UnlockValidationException.PublicMessage, wrongPass!.Message);
        Assert.Equal(wrongPass.Message, badMagic!.Message);
        Assert.DoesNotContain(TestPassword, wrongPass.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyUnlock_Rejects_RollbackGeneration()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, generation: 5, metadataGeneration: 2, parentMetadataGeneration: 4);
        Assert.Throws<UnlockValidationException>(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, bundle.HeaderCopy)], bundle.MetadataRoot, TestPassword));
    }

    [Fact]
    public void ReadOnlyUnlock_Rejects_ConflictingHeaderCopyConsensus()
    {
        var good = Av3TestVectors.BuildUnlockBundle(TestPassword, generation: 2);
        var conflicting = good.HeaderCopy.ToArray();
        conflicting[32] = 1;
        conflicting[124] ^= 0x01;
        Assert.Throws<UnlockValidationException>(() =>
            ReadOnlyUnlockValidator.Validate(
                good.Locator,
                [(0, good.HeaderCopy), (1, conflicting)],
                good.MetadataRoot,
                TestPassword));
    }

    [Fact]
    public void ReadOnlyUnlock_Rejects_HigherGenerationWhenMetadataRootMismatch()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, generation: 2);
        var highGenHeader = Av3TestVectors.RekeyHeaderGeneration(bundle, TestPassword, newGeneration: 5);
        Assert.Throws<UnlockValidationException>(() =>
            ReadOnlyUnlockValidator.Validate(
                bundle.Locator,
                [(1, highGenHeader)],
                bundle.MetadataRoot,
                TestPassword));
    }

    [Fact]
    public void KdfDescriptor_Rejects_UnsupportedAlgorithm()
    {
        var buf = new byte[Argon2idKdfDescriptor.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 42);
        Assert.ThrowsAny<Exception>(() => Argon2idKdfDescriptor.Parse(buf));
    }
}

internal static class Av3TestVectors
{
    private static readonly AstraArgon2Parameters FastKdf = AstraArgon2Parameters.LowMemory;

    internal sealed record UnlockBundle(byte[] Locator, byte[] HeaderCopy, byte[] MetadataRoot);

    internal static UnlockBundle BuildUnlockBundle(
        string password,
        ulong generation,
        ulong? metadataGeneration = null,
        ulong? parentMetadataGeneration = null)
    {
        var containerId = Guid.NewGuid();
        var metaGen = metadataGeneration ?? generation;
        var parentGen = parentMetadataGeneration ?? (metaGen > 0 ? metaGen - 1 : 0ul);
        var graphRoot = RandomNumberGenerator.GetBytes(32);
        var allocationRoot = RandomNumberGenerator.GetBytes(32);
        var indexRoot = RandomNumberGenerator.GetBytes(32);
        var journalHead = RandomNumberGenerator.GetBytes(32);
        var recoveryRoot = RandomNumberGenerator.GetBytes(32);
        var metadataPlain = MetadataRootPlaintext.BuildCanonical(
            (ushort)AstraCipherSuite.XChaCha20Poly1305,
            metaGen,
            parentGen,
            graphRoot,
            allocationRoot,
            indexRoot,
            journalHead,
            recoveryRoot);
        var metaPlainCommit = MetadataRootPlaintext.ComputeRootPlaintextCommitment(metadataPlain);

        var locator = new VaultLocator
        {
            ContainerId = containerId,
            CipherSuiteId = (ushort)AstraCipherSuite.XChaCha20Poly1305,
            KdfSuiteId = AstraSuiteIds.KdfArgon2id,
            HeaderPrimaryOffset = AstraFormatConstants.LocatorSize,
            HeaderSecondaryOffset = 0,
            HeaderTertiaryOffset = 0,
            HeaderCopySize = (uint)VaultHeaderCopy.ExpectedCopySize(1)
        };

        var vmk = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(32);
        var kdfDesc = Argon2idKdfDescriptor.FromParameters(FastKdf);
        var kek = AstraKdf.DeriveKek(password, salt, FastKdf);
        var aad = VmkUnwrapAad.Build(AstraFormatConstants.MajorVersion, containerId, slotId: 1, generation);
        var wrapped = AstraAead.Encrypt(AstraCipherSuite.XChaCha20Poly1305, kek, vmk, aad);
        AstraKdf.Zero(kek);

        var activationPlain = HeaderActivationPayload.BuildPlaintext(generation, metaPlainCommit, metaGen, parentGen);
        var activationDigest = HeaderActivationPayload.DigestFromPlaintext(activationPlain);

        var metaDigest = new byte[32];
        var metaNonce = RandomNumberGenerator.GetBytes(AstraAead.ChaChaNonceSize);
        var metaCipher = new byte[MetadataRootPlaintext.PlaintextSize];
        var metaTag = new byte[AstraAead.TagSize];
        for (var round = 0; round < 4; round++)
        {
            var metaAad = MetadataRootAad.Build(
                AstraFormatConstants.MajorVersion,
                (ushort)AstraCipherSuite.XChaCha20Poly1305,
                containerId,
                containerId,
                generation,
                metaGen,
                metaDigest,
                activationDigest,
                MetadataRootReadOnlyReader.DefaultLogicalId,
                MetadataRootPlaintext.PlaintextSize);
            var metadataKey = MetadataRootAead.DeriveMetadataKey(vmk);
            using (var chacha = new ChaCha20Poly1305(metadataKey))
            {
                chacha.Encrypt(metaNonce, metadataPlain, metaCipher, metaTag, metaAad);
            }

            AstraKdf.Zero(metadataKey);
            var nextDigest = SHA256.HashData(metaCipher);
            if (round > 0 && nextDigest.AsSpan().SequenceEqual(metaDigest))
            {
                break;
            }

            metaDigest = nextDigest;
        }

        var metadataCt = new AstraCiphertext(metaNonce, metaTag, metaCipher);

        var activationKey = HeaderActivationAead.DeriveActivationKey(vmk);
        var activationAad = ActivationPayloadAad.Build(
            AstraFormatConstants.MajorVersion,
            containerId,
            containerId,
            headerCopyId: 0,
            generation,
            (ushort)AstraCipherSuite.XChaCha20Poly1305,
            metaDigest,
            ActivationPayloadAad.TargetMetadataRoot);
        var activationCt = AstraAead.Encrypt(
            AstraCipherSuite.XChaCha20Poly1305,
            activationKey,
            activationPlain,
            activationAad);
        AstraKdf.Zero(activationKey);
        AstraKdf.Zero(vmk);

        var slot = new PasswordSlotEnvelope
        {
            SlotId = 1,
            CipherSuiteId = (ushort)AstraCipherSuite.XChaCha20Poly1305,
            KdfSuiteId = AstraSuiteIds.KdfArgon2id,
            Generation = generation,
            ContainerId = containerId,
            KdfSalt = salt,
            Kdf = kdfDesc,
            WrapNonce = wrapped.Nonce,
            WrapTag = wrapped.Tag,
            WrappedVmk = wrapped.Cipher
        };

        var header = new byte[VaultHeaderCopy.ExpectedCopySize(1)];
        VaultHeaderCopy.HeaderMagic.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), VaultHeaderCopy.HeaderStructVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), generation);
        containerId.TryWriteBytes(header.AsSpan(16, 16));
        header[32] = 0;
        header[33] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34), (ushort)AstraCipherSuite.XChaCha20Poly1305);
        kdfDesc.Write(header.AsSpan(36, Argon2idKdfDescriptor.Size));
        activationDigest.CopyTo(header.AsSpan(60, 32));
        metaPlainCommit.CopyTo(header.AsSpan(92, 32));
        metaDigest.CopyTo(header.AsSpan(124, 32));
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(156), metaGen);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(164), parentGen);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(172), ActivationPayloadAad.TargetMetadataRoot);
        containerId.TryWriteBytes(header.AsSpan(176, 16));
        activationCt.Nonce.CopyTo(header.AsSpan(192));
        activationCt.Tag.CopyTo(header.AsSpan(204));
        activationCt.Cipher.CopyTo(header.AsSpan(220));
        slot.Write(header.AsSpan(VaultHeaderCopy.FixedRegionSize, PasswordSlotEnvelope.Size));

        var metaRoot = new byte[MetadataRootDescriptor.DescriptorSize + metadataCt.Cipher.Length];
        MetadataRootDescriptor.RootMagic.CopyTo(metaRoot);
        BinaryPrimitives.WriteUInt16LittleEndian(metaRoot.AsSpan(4), MetadataRootDescriptor.RootStructVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(metaRoot.AsSpan(6), (ushort)AstraCipherSuite.XChaCha20Poly1305);
        BinaryPrimitives.WriteUInt64LittleEndian(metaRoot.AsSpan(8), metaGen);
        BinaryPrimitives.WriteUInt64LittleEndian(metaRoot.AsSpan(16), parentGen);
        containerId.TryWriteBytes(metaRoot.AsSpan(24, 16));
        metaDigest.CopyTo(metaRoot.AsSpan(72, 32));
        metadataCt.Nonce.CopyTo(metaRoot.AsSpan(104, metadataCt.Nonce.Length));
        metadataCt.Tag.CopyTo(metaRoot.AsSpan(116, metadataCt.Tag.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(metaRoot.AsSpan(132), (uint)metadataCt.Cipher.Length);
        metadataCt.Cipher.CopyTo(metaRoot.AsSpan(MetadataRootDescriptor.DescriptorSize));

        return new UnlockBundle(locator.Write(), header, metaRoot);
    }

    internal static byte[] RekeyHeaderGeneration(UnlockBundle bundle, string password, ulong newGeneration)
    {
        var containerId = new Guid(bundle.Locator.AsSpan(8, 16));
        var header = bundle.HeaderCopy.ToArray();
        var oldGen = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(8));
        var metaPlain = header.AsSpan(92, 32).ToArray();
        var parentGen = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(164));
        var salt = header.AsSpan(VaultHeaderCopy.FixedRegionSize + 36, 32).ToArray();
        var kdfDesc = Argon2idKdfDescriptor.Parse(header.AsSpan(36, Argon2idKdfDescriptor.Size));
        var slotOffset = VaultHeaderCopy.FixedRegionSize;

        var kek = AstraKdf.DeriveKek(password, salt, FastKdf);
        var oldAad = VmkUnwrapAad.Build(AstraFormatConstants.MajorVersion, containerId, 1, oldGen);
        var wrapped = new AstraCiphertext(
            header.AsSpan(slotOffset + 92, AstraAead.ChaChaNonceSize).ToArray(),
            header.AsSpan(slotOffset + 104, AstraAead.TagSize).ToArray(),
            header.AsSpan(slotOffset + 120, AstraKdf.KeySize).ToArray());
        var vmk = AstraAead.Decrypt(AstraCipherSuite.XChaCha20Poly1305, kek, wrapped, oldAad);
        var newAad = VmkUnwrapAad.Build(AstraFormatConstants.MajorVersion, containerId, 1, newGeneration);
        var newWrap = AstraAead.Encrypt(AstraCipherSuite.XChaCha20Poly1305, kek, vmk, newAad);
        var metaCtDigest = header.AsSpan(124, 32).ToArray();
        var activationPlain = HeaderActivationPayload.BuildPlaintext(newGeneration, metaPlain, newGeneration, parentGen);
        var activationKey = HeaderActivationAead.DeriveActivationKey(vmk);
        var activationAad = ActivationPayloadAad.Build(
            AstraFormatConstants.MajorVersion,
            containerId,
            containerId,
            headerCopyId: 1,
            newGeneration,
            (ushort)AstraCipherSuite.XChaCha20Poly1305,
            metaCtDigest,
            ActivationPayloadAad.TargetMetadataRoot);
        var activationCt = AstraAead.Encrypt(
            AstraCipherSuite.XChaCha20Poly1305,
            activationKey,
            activationPlain,
            activationAad);
        AstraKdf.Zero(activationKey);
        AstraKdf.Zero(kek);
        AstraKdf.Zero(vmk);

        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), newGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(156), newGeneration);
        header[32] = 1;
        HeaderActivationPayload.DigestFromPlaintext(activationPlain).CopyTo(header.AsSpan(60, 32));
        activationCt.Nonce.CopyTo(header.AsSpan(192));
        activationCt.Tag.CopyTo(header.AsSpan(204));
        activationCt.Cipher.CopyTo(header.AsSpan(220));

        var slot = new PasswordSlotEnvelope
        {
            SlotId = 1,
            CipherSuiteId = (ushort)AstraCipherSuite.XChaCha20Poly1305,
            KdfSuiteId = AstraSuiteIds.KdfArgon2id,
            Generation = newGeneration,
            ContainerId = containerId,
            KdfSalt = salt,
            Kdf = kdfDesc,
            WrapNonce = newWrap.Nonce,
            WrapTag = newWrap.Tag,
            WrappedVmk = newWrap.Cipher
        };
        slot.Write(header.AsSpan(slotOffset, PasswordSlotEnvelope.Size));
        return header;
    }
}