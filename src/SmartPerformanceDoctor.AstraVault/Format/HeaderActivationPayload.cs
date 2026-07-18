using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Format;

/// <summary>Authenticated activation payload commitment bound to metadata root.</summary>
public static class HeaderActivationPayload
{
    public const int PlaintextSize = 8 + 32 + 8 + 8;

    public static byte[] BuildPlaintext(
        ulong headerGeneration,
        ReadOnlySpan<byte> metadataRootPlaintextCommitment,
        ulong metadataGeneration,
        ulong parentMetadataGeneration)
    {
        if (metadataRootPlaintextCommitment.Length != 32)
        {
            throw new ArgumentException("Metadata root commitment must be 32 bytes.", nameof(metadataRootPlaintextCommitment));
        }

        var buf = new byte[PlaintextSize];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, headerGeneration);
        metadataRootPlaintextCommitment.CopyTo(buf.AsSpan(8));
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(40), metadataGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(48), parentMetadataGeneration);
        return buf;
    }

    public static byte[] Digest(
        ulong headerGeneration,
        ReadOnlySpan<byte> metadataRootPlaintextCommitment,
        ulong metadataGeneration,
        ulong parentMetadataGeneration)
    {
        var plain = BuildPlaintext(headerGeneration, metadataRootPlaintextCommitment, metadataGeneration, parentMetadataGeneration);
        return SHA256.HashData(plain);
    }

    public static byte[] DigestFromPlaintext(ReadOnlySpan<byte> plaintext) =>
        SHA256.HashData(plaintext);

    public static bool PlaintextMatchesHeader(VaultHeaderCopy copy, ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length != PlaintextSize)
        {
            return false;
        }

        var gen = BinaryPrimitives.ReadUInt64LittleEndian(plaintext);
        if (gen != copy.Generation)
        {
            return false;
        }

        if (!plaintext.Slice(8, 32).SequenceEqual(copy.MetadataRootPlaintextCommitment))
        {
            return false;
        }

        var metaGen = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.Slice(40));
        var parentGen = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.Slice(48));
        return metaGen == copy.MetadataGeneration && parentGen == copy.ParentMetadataGeneration;
    }
}