using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>
/// Append-only audit log with hash chaining (detects simple log truncation/rewrite).
/// Does not store secrets. Not a substitute for external SIEM / signed logs.
/// </summary>
public static class LabAuditChain
{
    public static void Append(string vaultRoot, string kind, string subject)
    {
        try
        {
            var dir = Path.Combine(vaultRoot, "audit");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "events.chain.log");
            var prev = "GENESIS";
            if (File.Exists(path))
            {
                var last = File.ReadLines(path).LastOrDefault();
                if (!string.IsNullOrWhiteSpace(last))
                {
                    var parts = last.Split('|');
                    if (parts.Length >= 1)
                    {
                        prev = parts[^1];
                    }
                }
            }

            var ts = DateTimeOffset.UtcNow.ToString("o");
            var body = $"{ts}|{Sanitize(kind)}|{Sanitize(subject)}";
            var hashInput = Encoding.UTF8.GetBytes(prev + "|" + body);
            var hash = Convert.ToHexString(SHA256.HashData(hashInput)).ToLowerInvariant();
            File.AppendAllText(path, body + "|" + hash + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // best effort
        }
    }

    public static IReadOnlyList<string> Verify(string vaultRoot)
    {
        var issues = new List<string>();
        var path = Path.Combine(vaultRoot, "audit", "events.chain.log");
        if (!File.Exists(path))
        {
            return issues;
        }

        var prev = "GENESIS";
        var lineNo = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 4)
            {
                issues.Add($"line {lineNo}: malformed");
                continue;
            }

            var hash = parts[^1];
            var body = string.Join('|', parts.Take(parts.Length - 1));
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prev + "|" + body)))
                .ToLowerInvariant();
            if (!LabCryptoCompare.FixedTimeEqualsHex(expected, hash))
            {
                issues.Add($"line {lineNo}: chain break");
            }

            prev = hash;
        }

        return issues;
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "-";
        }

        // strip pipes / control to keep chain format stable
        var cleaned = new string(s.Where(c => c is not ('|' or '\r' or '\n') && !char.IsControl(c)).ToArray());
        return cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }
}
