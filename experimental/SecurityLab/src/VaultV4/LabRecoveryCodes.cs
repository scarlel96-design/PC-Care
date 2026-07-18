using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>One-time recovery codes — hashes only on disk (lab).</summary>
public static class LabRecoveryCodes
{
    private sealed class Store
    {
        public string SaltHex { get; set; } = "";
        public List<Rec> Codes { get; set; } = new();
    }

    private sealed class Rec
    {
        public string HashHex { get; set; } = "";
        public bool Used { get; set; }
    }

    public static (IReadOnlyList<string> Plain, string Path) Generate(string vaultRoot)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var store = new Store { SaltHex = Convert.ToHexString(salt) };
        var plain = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var raw = RandomNumberGenerator.GetBytes(8);
            var code =
                $"{BitConverter.ToUInt16(raw, 0):X4}-{BitConverter.ToUInt16(raw, 2):X4}-{BitConverter.ToUInt16(raw, 4):X4}-{BitConverter.ToUInt16(raw, 6):X4}";
            plain.Add(code);
            store.Codes.Add(new Rec { HashHex = Hash(salt, code), Used = false });
        }

        var path = Path.Combine(vaultRoot, "recovery", "recovery_codes.v4.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
        return (plain, path);
    }

    /// <summary>
    /// Consume a single recovery code (marks used). Constant-time hash compare.
    /// Does not unlock VMK by itself — caller uses password path or rewrap policy.
    /// Lab: successful consume is treated as emergency proof for password reset workflows.
    /// </summary>
    public static bool TryConsume(string vaultRoot, string code, out string message)
    {
        message = "";
        var path = Path.Combine(vaultRoot, "recovery", "recovery_codes.v4.json");
        if (!File.Exists(path))
        {
            message = "복구 코드 저장소 없음";
            return false;
        }

        Store store;
        try
        {
            store = JsonSerializer.Deserialize<Store>(File.ReadAllText(path))
                    ?? throw new InvalidOperationException("parse");
        }
        catch
        {
            message = "복구 코드 저장소 손상";
            return false;
        }

        byte[] salt;
        try
        {
            salt = Convert.FromHexString(store.SaltHex);
        }
        catch
        {
            message = "복구 salt 손상";
            return false;
        }

        var candidate = Hash(salt, code ?? "");
        var matchIndex = -1;
        // Scan all entries with constant-time compare per slot (avoid early exit on match for timing side-channel reduction).
        for (var i = 0; i < store.Codes.Count; i++)
        {
            var rec = store.Codes[i];
            if (rec.Used)
            {
                // still compare against dummy to reduce branch leakage a bit
                _ = LabCryptoCompare.FixedTimeEqualsHex(rec.HashHex, candidate);
                continue;
            }

            if (LabCryptoCompare.FixedTimeEqualsHex(rec.HashHex, candidate))
            {
                matchIndex = i;
            }
        }

        if (matchIndex < 0)
        {
            message = "복구 코드가 올바르지 않거나 이미 사용됨";
            return false;
        }

        store.Codes[matchIndex].Used = true;
        File.WriteAllText(path, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
        message = "복구 코드 사용 처리됨 (일회용)";
        return true;
    }

    public static int Remaining(string vaultRoot)
    {
        var path = Path.Combine(vaultRoot, "recovery", "recovery_codes.v4.json");
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            var store = JsonSerializer.Deserialize<Store>(File.ReadAllText(path));
            return store?.Codes.Count(c => !c.Used) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string Hash(byte[] salt, string code)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData("pccare/lab/recovery/v4"u8.ToArray());
        h.AppendData(salt);
        h.AppendData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant()));
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
