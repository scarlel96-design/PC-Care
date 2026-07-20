using System.Security.Cryptography;
using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>
/// Records SHA-256 of lab assembly / vault critical files for tamper awareness.
/// Not a root of trust substitute for Authenticode.
/// </summary>
public static class LabIntegrityManifest
{
    public sealed class Report
    {
        public string CreatedUtc { get; init; } = "";
        public Dictionary<string, string> FileHashes { get; init; } = new();
    }

    public static Report BuildForDirectory(string directory, string searchPattern = "*")
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
        {
            return new Report { CreatedUtc = DateTimeOffset.UtcNow.ToString("o"), FileHashes = map };
        }

        foreach (var file in Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories))
        {
            try
            {
                var rel = Path.GetRelativePath(directory, file);
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
                map[rel.Replace('\\', '/')] = hash;
            }
            catch
            {
                // skip locked
            }
        }

        return new Report
        {
            CreatedUtc = DateTimeOffset.UtcNow.ToString("o"),
            FileHashes = map
        };
    }

    public static void Write(string vaultRoot, Report report)
    {
        var path = Path.Combine(vaultRoot, "integrity", "files.sha256.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static IReadOnlyList<string> Diff(Report expected, Report actual)
    {
        var issues = new List<string>();
        foreach (var (file, hash) in expected.FileHashes)
        {
            if (!actual.FileHashes.TryGetValue(file, out var now))
            {
                issues.Add("missing:" + file);
            }
            else if (!string.Equals(hash, now, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("changed:" + file);
            }
        }

        return issues;
    }
}
