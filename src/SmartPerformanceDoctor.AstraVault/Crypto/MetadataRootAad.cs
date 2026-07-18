using System.Buffers.Binary;
using System.Text;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>AAD for metadata-root ciphertext AEAD.</summary>
public static class MetadataRootAad
{
    public const string DomainLabel = "astra-metadata-root";

    public static byte[] Build(
        ushort formatVersion,
        ushort suiteId,
        Guid containerId,
        Guid vaultId,
        ulong headerGeneration,
        ulong metadataRootGeneration,
        ReadOnlySpan<byte> metadataRootCiphertextDigest,
        ReadOnlySpan<byte> activationPayloadDigest,
        ulong metadataRootLogicalId,
        uint storedCiphertextLength)
    {
        if (metadataRootCiphertextDigest.Length != 32)
        {
            throw new ArgumentException("Metadata root ciphertext digest must be 32 bytes.", nameof(metadataRootCiphertextDigest));
        }

        if (activationPayloadDigest.Length != 32)
        {
            throw new ArgumentException("Activation payload digest must be 32 bytes.", nameof(activationPayloadDigest));
        }

        var domain = Encoding.UTF8.GetBytes(DomainLabel);
        var buf = new byte[2 + 2 + 16 + 16 + 8 + 8 + 32 + 32 + 8 + 4 + 2 + domain.Length];
        var span = buf.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span, formatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(2), suiteId);
        containerId.TryWriteBytes(span.Slice(4, 16));
        vaultId.TryWriteBytes(span.Slice(20, 16));
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(36), headerGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(44), metadataRootGeneration);
        metadataRootCiphertextDigest.CopyTo(span.Slice(52, 32));
        activationPayloadDigest.CopyTo(span.Slice(84, 32));
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(116), metadataRootLogicalId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(124), storedCiphertextLength);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(128), (ushort)domain.Length);
        domain.CopyTo(span.Slice(130));
        return buf;
    }
}