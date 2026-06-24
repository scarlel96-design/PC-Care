using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models.SystemCare;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

internal static class CareAuditChain
{
    public static void InitializeManifest(string auditFolder, CareScanMode mode, IReadOnlyList<string> taskIds)
    {
        var manifest = new
        {
            protocol = "system-care-v2",
            mode = mode.ToString(),
            taskIds,
            createdAt = DateTimeOffset.Now.ToString("o"),
            chain = "GENESIS"
        };
        File.WriteAllText(
            Path.Combine(auditFolder, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
    }

    public static string Append(string auditFolder, string action, string detail, byte[]? hmacKey = null)
    {
        var logPath = Path.Combine(auditFolder, "audit_chain.log");
        var previous = ReadLastHash(auditFolder);
        var line = $"{DateTimeOffset.Now:o}|{action}|{detail}|{previous}";
        var hash = hmacKey is null
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))).ToLowerInvariant()
            : Convert.ToHexString(new HMACSHA256(hmacKey).ComputeHash(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();

        File.AppendAllText(logPath, $"{line}|{hash}\n", Encoding.UTF8);
        UpdateManifestChain(auditFolder, hash);
        return hash;
    }

    public static bool Verify(string auditFolder)
    {
        var logPath = Path.Combine(auditFolder, "audit_chain.log");
        if (!File.Exists(logPath))
        {
            return true;
        }

        var previous = "GENESIS";
        foreach (var raw in File.ReadAllLines(logPath))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 5)
            {
                return false;
            }

            if (!string.Equals(parts[^2], previous, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var embedded = parts[^1];
            var unsigned = string.Join('|', parts[..^1]);
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(unsigned))).ToLowerInvariant();
            if (!string.Equals(embedded, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            previous = embedded;
        }

        return true;
    }

    private static string ReadLastHash(string auditFolder)
    {
        var manifestPath = Path.Combine(auditFolder, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return "GENESIS";
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("chain", out var chain))
            {
                return chain.GetString() ?? "GENESIS";
            }
        }
        catch
        {
            // Fall through.
        }

        return "GENESIS";
    }

    private static void UpdateManifestChain(string auditFolder, string hash)
    {
        var manifestPath = Path.Combine(auditFolder, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            var text = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(text);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.NameEquals("chain"))
                    {
                        writer.WriteString("chain", hash);
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }

                if (!doc.RootElement.TryGetProperty("chain", out _))
                {
                    writer.WriteString("chain", hash);
                }

                writer.WriteEndObject();
            }

            File.WriteAllBytes(manifestPath, ms.ToArray());
        }
        catch
        {
            // Best-effort.
        }
    }
}