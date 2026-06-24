using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.App.Services.Commercial;

internal static class SecureDeleteAuditChain
{
    private static readonly byte[] KeyMagic = "SPDSDAK1"u8.ToArray();

    public static byte[] EnsureOperationKey(string operationId)
    {
        var keyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPerformanceDoctor",
            "secure_delete",
            "keys");
        Directory.CreateDirectory(keyDir);
        var keyPath = Path.Combine(keyDir, $"{operationId}.key");

        if (File.Exists(keyPath))
        {
            return Unprotect(File.ReadAllBytes(keyPath));
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(keyPath, Protect(key));
        return key;
    }

    public static string Append(
        string auditPath,
        string operationId,
        string pathHash,
        string contentHash,
        string protocol,
        string result,
        string previousHash,
        byte[] hmacKey)
    {
        var line = $"{DateTimeOffset.Now:o}|{operationId}|{pathHash}|{contentHash}|{protocol}|{result}|{previousHash}";
        using var hmac = new HMACSHA256(hmacKey);
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
        File.AppendAllText(auditPath, $"{line}|{signature}\n", Encoding.UTF8);
        return signature;
    }

    public static bool Verify(string auditPath, byte[] hmacKey)
    {
        if (!File.Exists(auditPath))
        {
            return true;
        }

        var previous = "GENESIS";
        foreach (var rawLine in File.ReadAllLines(auditPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 8)
            {
                return false;
            }

            var signature = parts[^1];
            var chainInput = string.Join('|', parts[..^1]);
            if (!string.Equals(parts[^2], previous, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var hmac = new HMACSHA256(hmacKey);
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(chainInput))).ToLowerInvariant();
            if (!string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            previous = signature;
        }

        return true;
    }

    private static byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, KeyMagic, DataProtectionScope.CurrentUser);

    private static byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, KeyMagic, DataProtectionScope.CurrentUser);
}