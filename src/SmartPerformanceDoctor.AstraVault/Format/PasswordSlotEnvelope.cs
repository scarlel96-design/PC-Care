using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;

namespace SmartPerformanceDoctor.AstraVault.Format;

/// <summary>Fixed 384-byte password slot envelope (parse-only in Phase B).</summary>
public sealed class PasswordSlotEnvelope
{
    public const int Size = 384;
    public static ReadOnlySpan<byte> SlotMagic => "PSLT"u8;
    public const ushort SlotStructVersion = 1;

    public ushort SlotId { get; init; }
    public ushort CipherSuiteId { get; init; }
    public ushort KdfSuiteId { get; init; }
    public ulong Generation { get; init; }
    public Guid ContainerId { get; init; }
    public byte[] KdfSalt { get; init; } = [];
    public Argon2idKdfDescriptor Kdf { get; init; } = null!;
    public byte[] WrapNonce { get; init; } = [];
    public byte[] WrapTag { get; init; } = [];
    public byte[] WrappedVmk { get; init; } = [];

    public static PasswordSlotEnvelope Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != Size)
        {
            throw new CryptographicException("Password slot size invalid.");
        }

        if (!data.Slice(0, 4).SequenceEqual(SlotMagic))
        {
            throw new CryptographicException("Password slot magic invalid.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4));
        if (version != SlotStructVersion)
        {
            throw new CryptographicException("Unsupported password slot version.");
        }

        var cipher = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8));
        var kdf = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(10));
        if (!AstraSuiteIds.IsSupportedCipher(cipher) || !AstraSuiteIds.IsSupportedKdf(kdf))
        {
            throw new CryptographicException("Unsupported password slot algorithm.");
        }

        for (var i = 152; i < Size; i++)
        {
            if (data[i] != 0)
            {
                throw new CryptographicException("Password slot reserved must be zero.");
            }
        }

        var nonceLen = cipher == (ushort)AstraCipherSuite.Aes256Gcm
            ? AstraAead.AesNonceSize
            : AstraAead.ChaChaNonceSize;

        return new PasswordSlotEnvelope
        {
            SlotId = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6)),
            CipherSuiteId = cipher,
            KdfSuiteId = kdf,
            Generation = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(12)),
            ContainerId = new Guid(data.Slice(20, 16)),
            KdfSalt = data.Slice(36, 32).ToArray(),
            Kdf = Argon2idKdfDescriptor.Parse(data.Slice(68, Argon2idKdfDescriptor.Size)),
            WrapNonce = data.Slice(92, nonceLen).ToArray(),
            WrapTag = data.Slice(104, AstraAead.TagSize).ToArray(),
            WrappedVmk = data.Slice(120, AstraKdf.KeySize).ToArray()
        };
    }

    public void Write(Span<byte> destination)
    {
        if (destination.Length != Size)
        {
            throw new ArgumentException("Password slot buffer size invalid.", nameof(destination));
        }

        destination.Clear();
        SlotMagic.CopyTo(destination);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4), SlotStructVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6), SlotId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8), CipherSuiteId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10), KdfSuiteId);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(12), Generation);
        ContainerId.TryWriteBytes(destination.Slice(20, 16));
        KdfSalt.CopyTo(destination.Slice(36, 32));
        Kdf.Write(destination.Slice(68, Argon2idKdfDescriptor.Size));
        WrapNonce.CopyTo(destination.Slice(92));
        WrapTag.CopyTo(destination.Slice(104, AstraAead.TagSize));
        WrappedVmk.CopyTo(destination.Slice(120, AstraKdf.KeySize));
    }
}