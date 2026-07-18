using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Format;

namespace SmartPerformanceDoctor.AstraVault.Journal;

/// <summary>JNAL fixed descriptor (256 bytes). No paths/filenames/plaintext metadata.</summary>
public sealed class Av3JournalDescriptor
{
    public const int DescriptorSize = 256;
    public static ReadOnlySpan<byte> Magic => "JNAL"u8;
    public const ushort StructVersion = 1;
    public const uint ActivationTargetMetadataRoot = 1;

    public ushort CipherSuiteId { get; init; }
    public Guid ContainerId { get; init; }
    public Guid TransactionId { get; init; }
    public ulong PreviousGeneration { get; init; }
    public ulong TargetGeneration { get; init; }
    public byte[] PreviousMetadataRootCiphertextDigest { get; init; } = [];
    public byte[] TargetMetadataRootCiphertextDigest { get; init; } = [];
    public byte[] ObjectWriteSetDigest { get; init; } = [];
    public byte[] MetadataWriteDigest { get; init; } = [];
    public uint ActivationTarget { get; init; }
    public Av3JournalState State { get; init; }
    public ulong MonotonicTimestampUtc { get; init; }
    public byte[] RecordDigest { get; init; } = [];

    public byte[] Write()
    {
        var buf = new byte[DescriptorSize];
        Magic.CopyTo(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), StructVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6), CipherSuiteId);
        ContainerId.TryWriteBytes(buf.AsSpan(8, 16));
        TransactionId.TryWriteBytes(buf.AsSpan(24, 16));
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(40), PreviousGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(48), TargetGeneration);
        PreviousMetadataRootCiphertextDigest.CopyTo(buf.AsSpan(56, 32));
        TargetMetadataRootCiphertextDigest.CopyTo(buf.AsSpan(88, 32));
        ObjectWriteSetDigest.CopyTo(buf.AsSpan(120, 32));
        MetadataWriteDigest.CopyTo(buf.AsSpan(152, 32));
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(184), ActivationTarget);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(188), (uint)State);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(192), MonotonicTimestampUtc);
        var digest = Av3JournalDigest.ComputeRecordDigest(buf);
        digest.CopyTo(buf.AsSpan(Av3JournalDigest.RecordDigestOffset, Av3JournalDigest.RecordDigestSize));
        return buf;
    }

    public static Av3JournalDescriptor Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != DescriptorSize)
        {
            throw new CryptographicException("Journal descriptor size invalid.");
        }

        if (!data.Slice(0, 4).SequenceEqual(Magic))
        {
            throw new CryptographicException("Journal magic invalid.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4));
        if (version != StructVersion)
        {
            throw new CryptographicException("Unsupported journal version.");
        }

        var cipher = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6));
        if (!AstraSuiteIds.IsSupportedCipher(cipher))
        {
            throw new CryptographicException("Unsupported journal cipher suite.");
        }

        for (var i = 232; i < DescriptorSize; i++)
        {
            if (data[i] != 0)
            {
                throw new CryptographicException("Journal reserved must be zero.");
            }
        }

        var embedded = data.Slice(Av3JournalDigest.RecordDigestOffset, Av3JournalDigest.RecordDigestSize);
        var computed = Av3JournalDigest.ComputeRecordDigest(data);
        if (!embedded.SequenceEqual(computed))
        {
            throw new CryptographicException("Journal record digest mismatch.");
        }

        return new Av3JournalDescriptor
        {
            CipherSuiteId = cipher,
            ContainerId = new Guid(data.Slice(8, 16)),
            TransactionId = new Guid(data.Slice(24, 16)),
            PreviousGeneration = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(40)),
            TargetGeneration = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(48)),
            PreviousMetadataRootCiphertextDigest = data.Slice(56, 32).ToArray(),
            TargetMetadataRootCiphertextDigest = data.Slice(88, 32).ToArray(),
            ObjectWriteSetDigest = data.Slice(120, 32).ToArray(),
            MetadataWriteDigest = data.Slice(152, 32).ToArray(),
            ActivationTarget = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(184)),
            State = (Av3JournalState)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(188)),
            MonotonicTimestampUtc = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(192)),
            RecordDigest = embedded.ToArray()
        };
    }
}