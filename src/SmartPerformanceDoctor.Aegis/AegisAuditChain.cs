using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisAuditChain
{
    private static readonly byte[] ChainKey = DeriveChainKey();

    public static AegisAuditEntry Append(
        string operationId,
        string action,
        string target,
        string result,
        string? previousHash = null,
        string? restoredHash = null)
    {
        if (!AegisMirrorPaths.EnsureLayout())
        {
            return new AegisAuditEntry
            {
                OperationId = operationId,
                Action = action,
                Target = target,
                Result = "layout-unavailable"
            };
        }

        var previous = previousHash ?? ReadLastHash() ?? "GENESIS";
        var entry = new AegisAuditEntry
        {
            EntryIndex = CountEntries() + 1,
            Timestamp = DateTimeOffset.Now,
            OperationId = operationId,
            Action = action,
            Target = target,
            RestoredHash = restoredHash ?? "",
            Result = result,
            PreviousEntryHash = previous
        };

        var payload = JsonSerializer.Serialize(entry with { CurrentEntryHash = "" });
        entry = entry with
        {
            CurrentEntryHash = Convert.ToHexString(
                HMACSHA256.HashData(ChainKey, Encoding.UTF8.GetBytes($"{previous}|{payload}"))).ToLowerInvariant()
        };

        File.AppendAllText(
            AegisMirrorPaths.AuditLogFile,
            JsonSerializer.Serialize(entry) + Environment.NewLine,
            Encoding.UTF8);
        return entry;
    }

    public static bool VerifyChain()
    {
        if (!File.Exists(AegisMirrorPaths.AuditLogFile))
        {
            return true;
        }

        var previous = "GENESIS";
        foreach (var line in File.ReadAllLines(AegisMirrorPaths.AuditLogFile))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            AegisAuditEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<AegisAuditEntry>(line);
            }
            catch
            {
                return false;
            }

            if (entry is null || !string.Equals(entry.PreviousEntryHash, previous, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = JsonSerializer.Serialize(entry with { CurrentEntryHash = "" });
            var expected = Convert.ToHexString(
                HMACSHA256.HashData(ChainKey, Encoding.UTF8.GetBytes($"{previous}|{payload}"))).ToLowerInvariant();
            if (!string.Equals(expected, entry.CurrentEntryHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            previous = entry.CurrentEntryHash;
        }

        return true;
    }

    private static int CountEntries()
    {
        if (!File.Exists(AegisMirrorPaths.AuditLogFile))
        {
            return 0;
        }

        return File.ReadAllLines(AegisMirrorPaths.AuditLogFile).Count(l => !string.IsNullOrWhiteSpace(l));
    }

    private static string? ReadLastHash()
    {
        if (!File.Exists(AegisMirrorPaths.AuditLogFile))
        {
            return null;
        }

        var last = File.ReadAllLines(AegisMirrorPaths.AuditLogFile).LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (string.IsNullOrWhiteSpace(last))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AegisAuditEntry>(last)?.CurrentEntryHash;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DeriveChainKey()
    {
        var material = Encoding.UTF8.GetBytes("astra-aegis-mirror-audit-v1");
        return HMACSHA256.HashData(SHA256.HashData(material), Encoding.UTF8.GetBytes(Environment.MachineName));
    }
}

public sealed record AegisAuditEntry
{
    public int EntryIndex { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string OperationId { get; init; } = "";
    public string Action { get; init; } = "";
    public string Target { get; init; } = "";
    public string RestoredHash { get; init; } = "";
    public string Result { get; init; } = "";
    public string PreviousEntryHash { get; init; } = "";
    public string CurrentEntryHash { get; init; } = "";
}