using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class PreservedExecutionLogStore
{
    public IReadOnlyList<StableLogEntry> LoadRecent(int maxItems = 500)
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "RepairLogs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "CrashLogs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "Reports"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "RepairAudits")
        };

        var files = new List<string>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            files.AddRange(Directory.GetFiles(root, "*.log", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(root, "*.html", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(root, "*.txt", SearchOption.AllDirectories));
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(maxItems)
            .Select(ToEntry)
            .ToArray();
    }

    private static StableLogEntry ToEntry(string path)
    {
        var info = new FileInfo(path);
        return new StableLogEntry(
            info.Name,
            DetectCategory(path),
            info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
            path,
            Preview(path),
            info.Length);
    }

    private static string DetectCategory(string path)
    {
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains("repairaudits")) return "repair-audit";
        if (normalized.Contains("repairlogs")) return "repair-log";
        if (normalized.Contains("crashlogs")) return "crash-log";
        if (normalized.Contains("reports")) return "report";
        return "log";
    }

    private static string Preview(string path)
    {
        try
        {
            var text = string.Join(" ", File.ReadLines(path).Take(6));
            text = text.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            return text.Length > 360 ? text[..360] + "…" : text;
        }
        catch
        {
            return "미리보기를 읽지 못했습니다. 파일 열기로 확인하세요.";
        }
    }
}
