using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Format;

/// <summary>Canonical metadata-root plaintext (512 bytes); no graph/manifest materialization.</summary>
public static class MetadataRootPlaintext
{
    public const int PlaintextSize = 512;
    public const int CommitmentOffset = 480;
    public const int CommitmentSize = 32;
    public const int ReservedStart = 184;
    public const int ReservedLength = CommitmentOffset - ReservedStart;

    public static ReadOnlySpan<byte> RootMagic => "MRPL"u8;
    public const ushort RootVersion = 1;

    public static byte[] BuildCanonical(
        ushort suiteId,
        ulong generation,
        ulong parentGeneration,
        ReadOnlySpan<byte> graphRootDigest,
        ReadOnlySpan<byte> allocationRootDigest,
        ReadOnlySpan<byte> indexRootDigest,
        ReadOnlySpan<byte> journalHeadCommitment,
        ReadOnlySpan<byte> recoveryRootDigest)
    {
        if (graphRootDigest.Length != 32
            || allocationRootDigest.Length != 32
            || indexRootDigest.Length != 32
            || journalHeadCommitment.Length != 32
            || recoveryRootDigest.Length != 32)
        {
            throw new ArgumentException("Metadata root digests must be 32 bytes.");
        }

        var buf = new byte[PlaintextSize];
        RootMagic.CopyTo(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), RootVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6), suiteId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(8), generation);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(16), parentGeneration);
        graphRootDigest.CopyTo(buf.AsSpan(24, 32));
        allocationRootDigest.CopyTo(buf.AsSpan(56, 32));
        indexRootDigest.CopyTo(buf.AsSpan(88, 32));
        journalHeadCommitment.CopyTo(buf.AsSpan(120, 32));
        recoveryRootDigest.CopyTo(buf.AsSpan(152, 32));
        ComputeRootPlaintextCommitment(buf).CopyTo(buf.AsSpan(CommitmentOffset, CommitmentSize));
        return buf;
    }

    public static byte[] ComputeRootPlaintextCommitment(ReadOnlySpan<byte> canonicalPlaintext)
    {
        if (canonicalPlaintext.Length != PlaintextSize)
        {
            throw new ArgumentException("Metadata root plaintext must be exactly 512 bytes.", nameof(canonicalPlaintext));
        }

        return SHA256.HashData(canonicalPlaintext.Slice(0, CommitmentOffset));
    }

    public static void ValidateCanonical(
        ReadOnlySpan<byte> plaintext,
        VaultHeaderCopy header,
        MetadataRootDescriptor descriptor)
    {
        if (plaintext.Length != PlaintextSize)
        {
            throw new UnlockValidationException();
        }

        if (!plaintext.Slice(0, 4).SequenceEqual(RootMagic))
        {
            throw new UnlockValidationException();
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(plaintext.Slice(4));
        if (version != RootVersion)
        {
            throw new UnlockValidationException();
        }

        var suite = BinaryPrimitives.ReadUInt16LittleEndian(plaintext.Slice(6));
        if (!AstraSuiteIds.IsSupportedCipher(suite) || suite != descriptor.CipherSuiteId)
        {
            throw new UnlockValidationException();
        }

        var generation = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.Slice(8));
        var parentGeneration = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.Slice(16));
        if (generation != descriptor.Generation
            || generation != header.MetadataGeneration
            || parentGeneration != descriptor.ParentGeneration
            || parentGeneration != header.ParentMetadataGeneration)
        {
            throw new UnlockValidationException();
        }

        if (generation < parentGeneration)
        {
            throw new UnlockValidationException();
        }

        for (var i = ReservedStart; i < CommitmentOffset; i++)
        {
            if (plaintext[i] != 0)
            {
                throw new UnlockValidationException();
            }
        }

        var embeddedCommitment = plaintext.Slice(CommitmentOffset, CommitmentSize);
        var computed = ComputeRootPlaintextCommitment(plaintext);
        if (!embeddedCommitment.SequenceEqual(computed)
            || !embeddedCommitment.SequenceEqual(header.MetadataRootPlaintextCommitment))
        {
            throw new UnlockValidationException();
        }
    }

    public static MetadataRootPlaintextFields ReadFields(ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length != PlaintextSize)
        {
            throw new ArgumentException("Metadata root plaintext must be exactly 512 bytes.", nameof(plaintext));
        }

        return new MetadataRootPlaintextFields
        {
            Generation = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.Slice(8)),
            ParentGeneration = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.Slice(16)),
            GraphRootDigest = plaintext.Slice(24, 32).ToArray(),
            AllocationRootDigest = plaintext.Slice(56, 32).ToArray(),
            IndexRootDigest = plaintext.Slice(88, 32).ToArray(),
            JournalHeadCommitment = plaintext.Slice(120, 32).ToArray(),
            RecoveryRootDigest = plaintext.Slice(152, 32).ToArray(),
            RootPlaintextCommitment = plaintext.Slice(CommitmentOffset, CommitmentSize).ToArray()
        };
    }
}

public sealed class MetadataRootPlaintextFields
{
    public ulong Generation { get; init; }
    public ulong ParentGeneration { get; init; }
    public byte[] GraphRootDigest { get; init; } = [];
    public byte[] AllocationRootDigest { get; init; } = [];
    public byte[] IndexRootDigest { get; init; } = [];
    public byte[] JournalHeadCommitment { get; init; } = [];
    public byte[] RecoveryRootDigest { get; init; } = [];
    public byte[] RootPlaintextCommitment { get; init; } = [];
}