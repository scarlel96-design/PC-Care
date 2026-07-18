using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Format;

/// <summary>Single vault.header copy (fixed header region + password slot section).</summary>
public sealed class VaultHeaderCopy
{
    public const int FixedRegionSize = 512;
    public static ReadOnlySpan<byte> HeaderMagic => "VHDR"u8;
    public const ushort HeaderStructVersion = 1;
    public const byte MaxPasswordSlots = 2;
    public const int ActivationRegionEnd = 284;
    public const int ActivationCiphertextSize = HeaderActivationPayload.PlaintextSize;

    public ushort StructVersion { get; init; }
    public ushort Flags { get; init; }
    public ulong Generation { get; init; }
    public Guid ContainerId { get; init; }
    public byte CopyIndex { get; init; }
    public byte PasswordSlotCount { get; init; }
    public ushort CipherSuiteId { get; init; }
    public Argon2idKdfDescriptor DefaultKdf { get; init; } = null!;
    public byte[] ActivationPayloadDigest { get; init; } = [];
    public byte[] MetadataRootPlaintextCommitment { get; init; } = [];
    public byte[] MetadataRootCiphertextDigest { get; init; } = [];
    public ulong MetadataGeneration { get; init; }
    public ulong ParentMetadataGeneration { get; init; }
    public uint ActivationTarget { get; init; }
    public Guid VaultId { get; init; }
    public byte[] ActivationNonce { get; init; } = [];
    public byte[] ActivationTag { get; init; } = [];
    public byte[] ActivationCiphertext { get; init; } = [];
    public IReadOnlyList<PasswordSlotEnvelope> PasswordSlots { get; init; } = [];

    public static int ExpectedCopySize(byte slotCount) =>
        FixedRegionSize + slotCount * PasswordSlotEnvelope.Size;

    public static VaultHeaderCopy Parse(ReadOnlySpan<byte> data, uint maxCopySize)
    {
        if (data.Length < FixedRegionSize || data.Length > maxCopySize)
        {
            throw new CryptographicException("Header copy size out of bounds.");
        }

        if (maxCopySize > AstraFormatConstants.MaxHeaderCopySize)
        {
            throw new CryptographicException("Header copy size out of bounds.");
        }

        if (!data.Slice(0, 4).SequenceEqual(HeaderMagic))
        {
            throw new CryptographicException("Header copy magic invalid.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4));
        if (version != HeaderStructVersion)
        {
            throw new CryptographicException("Unsupported header copy version.");
        }

        var cipher = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(34));
        if (!AstraSuiteIds.IsSupportedCipher(cipher))
        {
            throw new CryptographicException("Unsupported header cipher suite.");
        }

        var slotCount = data[33];
        if (slotCount == 0 || slotCount > MaxPasswordSlots)
        {
            throw new CryptographicException("Header password slot count invalid.");
        }

        var expected = ExpectedCopySize(slotCount);
        Av3ParserGuard.RejectTrailingBytes(data, expected);

        var (activationNonceOffset, nonceLen, activationTagOffset) = Crypto.Av3AeadOnDiskLayout.HeaderActivationOffsets(cipher);
        var activationCipherOffset = activationTagOffset + Crypto.AstraAead.TagSize;
        var activationRegionEnd = activationCipherOffset + ActivationCiphertextSize;
        if (activationRegionEnd > FixedRegionSize)
        {
            throw new CryptographicException("Header activation region out of bounds.");
        }

        Av3ParserGuard.RequireReservedZero(data.Slice(activationRegionEnd, FixedRegionSize - activationRegionEnd));

        var slots = new List<PasswordSlotEnvelope>(slotCount);
        var offset = FixedRegionSize;
        for (var i = 0; i < slotCount; i++)
        {
            slots.Add(PasswordSlotEnvelope.Parse(data.Slice(offset, PasswordSlotEnvelope.Size)));
            offset += PasswordSlotEnvelope.Size;
        }

        return new VaultHeaderCopy
        {
            StructVersion = version,
            Flags = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6)),
            Generation = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8)),
            ContainerId = new Guid(data.Slice(16, 16)),
            CopyIndex = data[32],
            PasswordSlotCount = slotCount,
            CipherSuiteId = cipher,
            DefaultKdf = Argon2idKdfDescriptor.Parse(data.Slice(36, Argon2idKdfDescriptor.Size)),
            ActivationPayloadDigest = data.Slice(60, 32).ToArray(),
            MetadataRootPlaintextCommitment = data.Slice(92, 32).ToArray(),
            MetadataRootCiphertextDigest = data.Slice(124, 32).ToArray(),
            MetadataGeneration = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(156)),
            ParentMetadataGeneration = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(164)),
            ActivationTarget = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(172)),
            VaultId = new Guid(data.Slice(176, 16)),
            ActivationNonce = data.Slice(activationNonceOffset, nonceLen).ToArray(),
            ActivationTag = data.Slice(activationTagOffset, Crypto.AstraAead.TagSize).ToArray(),
            ActivationCiphertext = data.Slice(activationCipherOffset, ActivationCiphertextSize).ToArray(),
            PasswordSlots = slots
        };
    }
}