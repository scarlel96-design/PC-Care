using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Validation;

/// <summary>Fail-closed parser helpers (no panics, bounded lengths).</summary>
internal static class Av3ParserGuard
{
    public static void RequireExactLength(ReadOnlySpan<byte> data, int expected)
    {
        if (data.Length != expected)
        {
            throw new CryptographicException("Length invalid.");
        }
    }

    public static void RequireMaxLength(ReadOnlySpan<byte> data, int maxInclusive)
    {
        if (data.Length > maxInclusive)
        {
            throw new CryptographicException("Length oversized.");
        }
    }

    public static void RequireReservedZero(ReadOnlySpan<byte> reserved)
    {
        foreach (var b in reserved)
        {
            if (b != 0)
            {
                throw new CryptographicException("Reserved must be zero.");
            }
        }
    }

    public static int CheckedAdd(int a, int b)
    {
        if (b > int.MaxValue - a)
        {
            throw new CryptographicException("Length overflow.");
        }

        return a + b;
    }

    public static void RejectTrailingBytes(ReadOnlySpan<byte> data, int consumed)
    {
        if (consumed < 0 || consumed > data.Length)
        {
            throw new CryptographicException("Length invalid.");
        }

        if (consumed != data.Length)
        {
            throw new CryptographicException("Trailing bytes rejected.");
        }
    }
}