using System.Text.Json;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class RepairExecutionAuditStore
{
    public string AuditRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "RepairAudits");

    public string Save(RepairVerificationResult result)
    {
        Directory.CreateDirectory(AuditRoot);

        var safeOperation = string.Concat(result.OperationId.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        var path = Path.Combine(AuditRoot, $"repair_audit_{DateTimeOffset.Now:yyyyMMdd_HHmmss}_{safeOperation}.json");

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
        return path;
    }

    public IReadOnlyList<string> LoadRecentAuditPaths()
    {
        if (!Directory.Exists(AuditRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(AuditRoot, "repair_audit_*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(50)
            .ToArray();
    }
}
