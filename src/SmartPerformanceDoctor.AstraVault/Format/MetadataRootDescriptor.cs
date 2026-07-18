using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Format;

/// <summary>metadata.root.enc leading descriptor (256-byte header; ciphertext follows on disk).</summary>
public sealed class MetadataRootDescriptor
{
    public const int DescriptorSize = 256;
    public const uint MaxCiphertextLength = 16 * 1024 * 1024;
    public static ReadOnlySpan<byte> RootMagic => "MROT"u8;
    public const ushort RootStructVersion = 1;

    public ushort CipherSuiteId { get; init; }
    public ulong Generation { get; init; }
    public ulong ParentGeneration { get; init; }
    public Guid ContainerId { get; init; }
    public byte[] ParentRootHash { get; init; } = [];
    public byte[] MetadataCiphertextDigest { get; init; } = [];
    public byte[] Nonce { get; init; } = [];
    public byte[] Tag { get; init; } = [];
    public uint CiphertextLength { get; init; }

    public static MetadataRootDescriptor Parse(ReadOnlySpan<byte> data)
    {
        Av3ParserGuard.RequireExactLength(data, DescriptorSize);

        if (!data.Slice(0, 4).SequenceEqual(RootMagic))
        {
            throw new CryptographicException("Metadata root magic invalid.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4));
        if (version != RootStructVersion)
        {
            throw new CryptographicException("Unsupported metadata root version.");
        }

        var cipher = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6));
        if (!AstraSuiteIds.IsSupportedCipher(cipher))
        {
            throw new CryptographicException("Unsupported metadata cipher suite.");
        }

        var (nonceOffset, nonceLen, tagOffset, ctLenOffset) = Crypto.Av3AeadOnDiskLayout.MetadataRootNonceTagOffsets(cipher);
        var ctLen = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(ctLenOffset));
        if (ctLen > MaxCiphertextLength)
        {
            throw new CryptographicException("Metadata root ciphertext oversized.");
        }

        for (var i = ctLenOffset + 4; i < DescriptorSize; i++)
        {
            if (data[i] != 0)
            {
                throw new CryptographicException("Metadata root reserved must be zero.");
            }
        }

        return new MetadataRootDescriptor
        {
            CipherSuiteId = cipher,
            Generation = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8)),
            ParentGeneration = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(16)),
            ContainerId = new Guid(data.Slice(24, 16)),
            ParentRootHash = data.Slice(40, 32).ToArray(),
            MetadataCiphertextDigest = data.Slice(72, 32).ToArray(),
            Nonce = data.Slice(nonceOffset, nonceLen).ToArray(),
            Tag = data.Slice(tagOffset, Crypto.AstraAead.TagSize).ToArray(),
            CiphertextLength = ctLen
        };
    }
}