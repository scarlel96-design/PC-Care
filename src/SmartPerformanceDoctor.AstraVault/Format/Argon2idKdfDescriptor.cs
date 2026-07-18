using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Format;

/// <summary>24-byte Argon2id KDF descriptor (on-disk).</summary>
public sealed class Argon2idKdfDescriptor
{
    public const int Size = 24;
    public const ushort DescriptorVersion = 1;

    public ushort KdfSuiteId { get; init; }
    public ushort ProfileId { get; init; }
    public uint MemoryKiB { get; init; }
    public uint Iterations { get; init; }
    public uint Parallelism { get; init; }

    public Crypto.AstraArgon2Parameters ToParameters() =>
        new((int)MemoryKiB, (int)Iterations, (int)Parallelism,
            ProfileId == 2 ? Crypto.AstraKdfProfile.LowMemoryFallback : Crypto.AstraKdfProfile.Standard);

    public static Argon2idKdfDescriptor FromParameters(Crypto.AstraArgon2Parameters p) =>
        new()
        {
            KdfSuiteId = AstraSuiteIds.KdfArgon2id,
            ProfileId = p.Profile == Crypto.AstraKdfProfile.LowMemoryFallback ? (ushort)2 : (ushort)1,
            MemoryKiB = (uint)p.MemoryKiB,
            Iterations = (uint)p.Iterations,
            Parallelism = (uint)p.Parallelism
        };

    public static Argon2idKdfDescriptor Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != Size)
        {
            throw new CryptographicException("KDF descriptor size invalid.");
        }

        var suite = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if (!AstraSuiteIds.IsSupportedKdf(suite))
        {
            throw new CryptographicException("Unsupported KDF suite.");
        }

        for (var i = 20; i < Size; i++)
        {
            if (data[i] != 0)
            {
                throw new CryptographicException("KDF descriptor reserved must be zero.");
            }
        }

        var descriptor = new Argon2idKdfDescriptor
        {
            KdfSuiteId = suite,
            ProfileId = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2)),
            MemoryKiB = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4)),
            Iterations = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8)),
            Parallelism = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12))
        };

        if (!descriptor.ToParameters().MeetsMinimum)
        {
            throw new CryptographicException("KDF below minimum.");
        }

        return descriptor;
    }

    public void Write(Span<byte> destination)
    {
        if (destination.Length != Size)
        {
            throw new ArgumentException("KDF descriptor buffer size invalid.", nameof(destination));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, KdfSuiteId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(2), ProfileId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4), MemoryKiB);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8), Iterations);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(12), Parallelism);
    }
}