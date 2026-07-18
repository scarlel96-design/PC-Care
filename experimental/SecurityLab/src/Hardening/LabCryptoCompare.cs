using System.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>Constant-time comparison helpers (secret / integrity values).</summary>
public static class LabCryptoCompare
{
    public static bool FixedTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b) =>
        a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);

    public static bool FixedTimeEqualsHex(string? expectedHex, string? actualHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex) || string.IsNullOrWhiteSpace(actualHex))
        {
            return false;
        }

        try
        {
            var a = Convert.FromHexString(expectedHex.Trim());
            var b = Convert.FromHexString(actualHex.Trim());
            return FixedTimeEquals(a, b);
        }
        catch
        {
            return false;
        }
    }

    public static bool FixedTimeEqualsUtf8(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        try
        {
            return FixedTimeEquals(ba, bb);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ba);
            CryptographicOperations.ZeroMemory(bb);
        }
    }
}
