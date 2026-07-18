using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.PartialWrite;

/// <summary>Simulates partial/torn writes on in-memory blobs (no user vault paths).</summary>
public static class Av3TornWriteSimulator
{
    public static byte[] Apply(ReadOnlySpan<byte> pristine, Av3PartialWriteScenario scenario)
    {
        if (pristine.Length == 0)
        {
            return [];
        }

        return scenario.Mode switch
        {
            Av3PartialWriteMode.Truncation => Truncate(pristine, scenario.Parameter),
            Av3PartialWriteMode.TrailingGarbageAppend => AppendGarbage(pristine, scenario.Parameter),
            Av3PartialWriteMode.ZeroFilledTail => ZeroTail(pristine, scenario.Parameter),
            Av3PartialWriteMode.RandomByteCorruption => Corrupt(pristine, scenario.Parameter),
            Av3PartialWriteMode.SectorBoundarySplit => SectorSplit(pristine, scenario.SectorSize),
            _ => pristine.ToArray()
        };
    }

    private static byte[] Truncate(ReadOnlySpan<byte> data, int keep)
    {
        keep = Math.Clamp(keep, 1, Math.Max(1, data.Length - 1));
        return data.Slice(0, keep).ToArray();
    }

    private static byte[] AppendGarbage(ReadOnlySpan<byte> data, int garbageLength)
    {
        garbageLength = Math.Max(4, garbageLength);
        var tail = RandomNumberGenerator.GetBytes(garbageLength);
        var result = new byte[data.Length + tail.Length];
        data.CopyTo(result);
        tail.CopyTo(result.AsSpan(data.Length));
        return result;
    }

    private static byte[] ZeroTail(ReadOnlySpan<byte> data, int zeroFrom)
    {
        var result = data.ToArray();
        zeroFrom = Math.Clamp(zeroFrom, 0, result.Length - 1);
        result.AsSpan(zeroFrom).Clear();
        return result;
    }

    private static byte[] Corrupt(ReadOnlySpan<byte> data, int offset)
    {
        var result = data.ToArray();
        offset = Math.Clamp(offset, 0, result.Length - 1);
        result[offset] ^= 0xFF;
        return result;
    }

    private static byte[] SectorSplit(ReadOnlySpan<byte> data, int sectorSize)
    {
        sectorSize = Math.Max(64, sectorSize);
        if (data.Length <= sectorSize)
        {
            return Truncate(data, Math.Max(1, data.Length / 2));
        }

        return data.Slice(0, sectorSize).ToArray();
    }
}