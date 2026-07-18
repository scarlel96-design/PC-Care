using System.Buffers.Binary;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>AAD for header activation payload AEAD.</summary>
public static class ActivationPayloadAad
{
    public const uint TargetMetadataRoot = 1;

    public static byte[] Build(
        ushort formatVersion,
        Guid containerId,
        Guid vaultId,
        byte headerCopyId,
        ulong headerGeneration,
        ushort suiteId,
        ReadOnlySpan<byte> metadataRootDigest,
        uint activationTarget)
    {
        if (metadataRootDigest.Length != 32)
        {
            throw new ArgumentException("Metadata root digest must be 32 bytes.", nameof(metadataRootDigest));
        }

        var buf = new byte[2 + 16 + 16 + 1 + 3 + 8 + 2 + 2 + 32 + 4];
        var span = buf.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span, formatVersion);
        containerId.TryWriteBytes(span.Slice(2, 16));
        vaultId.TryWriteBytes(span.Slice(18, 16));
        span[34] = headerCopyId;
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(38), headerGeneration);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(46), suiteId);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(48), 0);
        metadataRootDigest.CopyTo(span.Slice(50, 32));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(82), activationTarget);
        return buf;
    }
}