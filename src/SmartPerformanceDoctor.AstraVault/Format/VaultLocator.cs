using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Format;

public static class AstraFormatConstants
{
    public const int LocatorSize = 512;
    public const ushort MajorVersion = 3;
    public const ushort MinorVersion = 0;
    public const uint MinHeaderCopySize = 896; // 512 + one PSLT slot
    public const uint MaxHeaderCopySize = 8192;
    public static ReadOnlySpan<byte> LocatorMagic => "AVLT"u8;
}

public sealed class VaultLocator
{
    public Guid ContainerId { get; init; }
    public ushort CipherSuiteId { get; init; }
    public ushort KdfSuiteId { get; init; }
    public ulong HeaderPrimaryOffset { get; init; }
    public ulong HeaderSecondaryOffset { get; init; }
    public ulong HeaderTertiaryOffset { get; init; }
    public uint HeaderCopySize { get; init; }

    public static VaultLocator CreateNew(ushort cipherSuite, ushort kdfSuite)
    {
        return new VaultLocator
        {
            ContainerId = Guid.NewGuid(),
            CipherSuiteId = cipherSuite,
            KdfSuiteId = kdfSuite,
            HeaderPrimaryOffset = AstraFormatConstants.LocatorSize,
            HeaderSecondaryOffset = 0,
            HeaderTertiaryOffset = 0,
            HeaderCopySize = 4096
        };
    }

    public byte[] Write()
    {
        var buf = new byte[AstraFormatConstants.LocatorSize];
        AstraFormatConstants.LocatorMagic.CopyTo(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), AstraFormatConstants.MajorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6), AstraFormatConstants.MinorVersion);
        ContainerId.TryWriteBytes(buf.AsSpan(8));
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(24), CipherSuiteId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(26), KdfSuiteId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(28), HeaderPrimaryOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(36), HeaderSecondaryOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(44), HeaderTertiaryOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(52), HeaderCopySize);
        return buf;
    }

    public static VaultLocator Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != AstraFormatConstants.LocatorSize)
        {
            throw new CryptographicException("Locator size invalid.");
        }

        if (!data.Slice(0, 4).SequenceEqual(AstraFormatConstants.LocatorMagic))
        {
            throw new CryptographicException("Locator magic invalid.");
        }

        var major = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4));
        if (major != AstraFormatConstants.MajorVersion)
        {
            throw new CryptographicException("Unsupported locator major version.");
        }

        for (var i = 56; i < AstraFormatConstants.LocatorSize; i++)
        {
            if (data[i] != 0)
            {
                throw new CryptographicException("Locator reserved bytes must be zero.");
            }
        }

        var cipher = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(24));
        var kdf = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(26));
        if (!AstraSuiteIds.IsSupportedCipher(cipher) || !AstraSuiteIds.IsSupportedKdf(kdf))
        {
            throw new CryptographicException("Unsupported locator cipher or KDF suite.");
        }

        var headerCopySize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(52));
        if (headerCopySize < AstraFormatConstants.MinHeaderCopySize
            || headerCopySize > AstraFormatConstants.MaxHeaderCopySize)
        {
            throw new CryptographicException("Locator header copy size out of bounds.");
        }

        var containerId = new Guid(data.Slice(8, 16));
        return new VaultLocator
        {
            ContainerId = containerId,
            CipherSuiteId = cipher,
            KdfSuiteId = kdf,
            HeaderPrimaryOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(28)),
            HeaderSecondaryOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(36)),
            HeaderTertiaryOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(44)),
            HeaderCopySize = headerCopySize
        };
    }
}