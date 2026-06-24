using System.Text.Json;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class ReportStore
{
    public string ReportsRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "Reports");

    public IReadOnlyList<ReportSummary> LoadRecentReports()
    {
        if (!Directory.Exists(ReportsRoot))
        {
            return Array.Empty<ReportSummary>();
        }

        return Directory.GetDirectories(ReportsRoot)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .Take(30)
            .Select(ToSummary)
            .Where(x => x is not null)
            .Cast<ReportSummary>()
            .ToArray();
    }

    private static ReportSummary? ToSummary(string dir)
    {
        var html = Path.Combine(dir, "report.html");
        var json = Path.Combine(dir, "report.json");
        var txt = Path.Combine(dir, "summary.txt");

        if (!File.Exists(html) && !File.Exists(json) && !File.Exists(txt))
        {
            return null;
        }

        var title = Path.GetFileName(dir);
        var module = GuessModuleFromFolder(dir);
        var status = "unknown";
        var summary = "";
        var created = Directory.GetCreationTime(dir).ToString("yyyy-MM-dd HH:mm");
        var actionsTakenCount = 0;

        try
        {
            if (File.Exists(json))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(json));
                var root = doc.RootElement;

                if (root.TryGetProperty("title", out var titleNode))
                {
                    title = titleNode.GetString() ?? title;
                }

                if (root.TryGetProperty("module", out var moduleNode))
                {
                    module = moduleNode.GetString() ?? module;
                }

                if (root.TryGetProperty("status", out var statusNode))
                {
                    status = statusNode.GetString() ?? status;
                }

                if (root.TryGetProperty("summary", out var summaryNode))
                {
                    summary = summaryNode.GetString() ?? summary;
                }

                if (root.TryGetProperty("createdAt", out var createdNode))
                {
                    created = FormatCreatedAt(createdNode.GetString()) ?? created;
                }

                if (root.TryGetProperty("actionsTaken", out var takenNode) && takenNode.ValueKind == JsonValueKind.Array)
                {
                    actionsTakenCount = takenNode.GetArrayLength();
                }
            }
        }
        catch
        {
            // Broken report files should not crash the app.
        }

        return new ReportSummary(
            title,
            TranslateModule(module),
            status,
            TranslateStatus(status),
            string.IsNullOrWhiteSpace(summary) ? "요약 없음" : summary,
            created,
            actionsTakenCount,
            html,
            json,
            txt);
    }

    private static string GuessModuleFromFolder(string dir)
    {
        var name = Path.GetFileName(dir);
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[^1] : "점검";
    }

    private static string? FormatCreatedAt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.StartsWith("unix:", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(raw.AsSpan(5), out var unix))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }

        return raw;
    }

    private static string TranslateModule(string module) => module.ToLowerInvariant() switch
    {
        "quick" => "빠른 점검",
        "system" => "시스템",
        "driver" => "드라이버",
        "audio" => "오디오",
        "full" => "전체",
        _ => module
    };

    public static string TranslateStatus(string status)
    {
        var s = status.ToLowerInvariant();
        if (s.Contains("ok") || s.Contains("양호") || s.Contains("정상") || s.Contains("healthy"))
        {
            return "양호";
        }

        if (s.Contains("critical") || s.Contains("위험") || s.Contains("fail") || s.Contains("error"))
        {
            return "위험";
        }

        if (s.Contains("warn") || s.Contains("주의") || s.Contains("caution"))
        {
            return "주의";
        }

        return status;
    }
}