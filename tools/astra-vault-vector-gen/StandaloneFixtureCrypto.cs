using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace AstraVaultVectorGen;

/// <summary>Independent TEST ONLY encoder (not shared with SmartPerformanceDoctor.AstraVault).</summary>
internal static class StandaloneFixtureCrypto
{
    public const int LocatorSize = 512;
    public const int HeaderFixed = 512;
    public const int SlotSize = 384;
    public const int MetadataDescriptorSize = 256;
    public const int ChaChaNonceSize = 12;
    public const int TagSize = 16;
    public const int KeySize = 32;
    public const int ActivationPlainSize = 56;
    public const int MetadataPlainSize = 512;
    public const int MetadataCommitmentOffset = 480;
    public const string MetadataRootAadDomain = "astra-metadata-root";

    public static byte[] ParseHex(string hex)
    {
        if (hex.Length % 2 != 0)
        {
            throw new ArgumentException("Invalid hex.", nameof(hex));
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    public static byte[] DeriveKek(string password, byte[] salt, Argon2Input p)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = p.Parallelism,
            MemorySize = p.MemoryKiB,
            Iterations = p.Iterations
        };
        return argon2.GetBytes(KeySize);
    }

    public static byte[] HkdfDomain(ReadOnlySpan<byte> masterKey, string domain)
    {
        var info = Encoding.UTF8.GetBytes(domain);
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey.ToArray(), KeySize, Array.Empty<byte>(), info);
    }

    public static void ChaChaEncrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plain, ReadOnlySpan<byte> aad, Span<byte> cipher, Span<byte> tag)
    {
        using var chacha = new ChaCha20Poly1305(key);
        chacha.Encrypt(nonce, plain, cipher, tag, aad);
    }

    public static byte[] BuildVmkUnwrapAad(ushort formatVersion, Guid containerId, ushort slotId, ulong generation)
    {
        var domain = Encoding.UTF8.GetBytes("astra-vmk-unwrap");
        var buf = new byte[2 + 2 + 2 + 2 + 8 + 16 + domain.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, formatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), slotId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(8), generation);
        containerId.TryWriteBytes(buf.AsSpan(16, 16));
        domain.CopyTo(buf.AsSpan(32));
        return buf;
    }

    public static byte[] BuildActivationAad(
        ushort formatVersion,
        Guid containerId,
        Guid vaultId,
        byte copyId,
        ulong generation,
        ushort suiteId,
        byte[] metadataDigest,
        uint activationTarget)
    {
        var buf = new byte[86];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, formatVersion);
        containerId.TryWriteBytes(buf.AsSpan(2, 16));
        vaultId.TryWriteBytes(buf.AsSpan(18, 16));
        buf[34] = copyId;
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(38), generation);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(46), suiteId);
        metadataDigest.CopyTo(buf.AsSpan(50, 32));
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(82), activationTarget);
        return buf;
    }

    public static byte[] BuildActivationPlain(ulong gen, byte[] plainCommit, ulong metaGen, ulong parentGen)
    {
        var buf = new byte[ActivationPlainSize];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, gen);
        plainCommit.CopyTo(buf.AsSpan(8, 32));
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(40), metaGen);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(48), parentGen);
        return buf;
    }

    public static byte[] BuildMetadataRootPlaintext(
        ushort suiteId,
        ulong generation,
        ulong parentGeneration,
        byte[] graphRoot,
        byte[] allocationRoot,
        byte[] indexRoot,
        byte[] journalHead,
        byte[] recoveryRoot)
    {
        var buf = new byte[MetadataPlainSize];
        "MRPL"u8.CopyTo(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6), suiteId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(8), generation);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(16), parentGeneration);
        graphRoot.CopyTo(buf.AsSpan(24, 32));
        allocationRoot.CopyTo(buf.AsSpan(56, 32));
        indexRoot.CopyTo(buf.AsSpan(88, 32));
        journalHead.CopyTo(buf.AsSpan(120, 32));
        recoveryRoot.CopyTo(buf.AsSpan(152, 32));
        var commitment = SHA256.HashData(buf.AsSpan(0, MetadataCommitmentOffset));
        commitment.CopyTo(buf.AsSpan(MetadataCommitmentOffset, 32));
        return buf;
    }

    public static byte[] BuildMetadataRootAad(
        ushort formatVersion,
        ushort suiteId,
        Guid containerId,
        Guid vaultId,
        ulong headerGeneration,
        ulong metadataGeneration,
        byte[] metadataCiphertextDigest,
        byte[] activationPayloadDigest,
        ulong logicalId,
        uint ciphertextLength)
    {
        var domain = Encoding.UTF8.GetBytes(MetadataRootAadDomain);
        var buf = new byte[2 + 2 + 16 + 16 + 8 + 8 + 32 + 32 + 8 + 4 + 2 + domain.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, formatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2), suiteId);
        containerId.TryWriteBytes(buf.AsSpan(4, 16));
        vaultId.TryWriteBytes(buf.AsSpan(20, 16));
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(36), headerGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(44), metadataGeneration);
        metadataCiphertextDigest.CopyTo(buf.AsSpan(52, 32));
        activationPayloadDigest.CopyTo(buf.AsSpan(84, 32));
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(116), logicalId);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(124), ciphertextLength);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(128), (ushort)domain.Length);
        domain.CopyTo(buf.AsSpan(130));
        return buf;
    }
}