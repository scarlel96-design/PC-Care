using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>
/// External security rule packs must be HMAC-signed before apply.
/// Prevents unsigned remote policy injection. Not AI-executable.
/// </summary>
public static class LabRulePack
{
    public sealed class Pack
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string PayloadJson { get; set; } = "";
        public string SignatureHex { get; set; } = "";
    }

    public static string Sign(string payloadJson, byte[] hmacKey)
    {
        using var h = new HMACSHA256(hmacKey);
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payloadJson)));
    }

    public static bool TryVerify(Pack pack, byte[] hmacKey, out string reason)
    {
        reason = "";
        if (pack is null || string.IsNullOrWhiteSpace(pack.PayloadJson))
        {
            reason = "empty pack";
            return false;
        }

        if (string.IsNullOrWhiteSpace(pack.SignatureHex))
        {
            reason = "unsigned rule pack rejected";
            return false;
        }

        var expected = Sign(pack.PayloadJson, hmacKey);
        if (!LabCryptoCompare.FixedTimeEqualsHex(expected, pack.SignatureHex))
        {
            reason = "rule pack signature mismatch";
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(pack.PayloadJson);
        }
        catch
        {
            reason = "invalid payload json";
            return false;
        }

        return true;
    }

    public static Pack CreateSigned(string id, int version, string payloadJson, byte[] hmacKey) =>
        new()
        {
            Id = id,
            Version = version,
            PayloadJson = payloadJson,
            SignatureHex = Sign(payloadJson, hmacKey)
        };
}
