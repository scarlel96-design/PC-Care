using System.Text.Json;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

/// <summary>
/// Provides one report feed for the dashboard, the report page, and the history center.
/// Core-engine reports and the newer System/Unified Care audit reports intentionally use
/// different layouts, so this store normalizes both rather than silently dropping either.
/// </summary>
public sealed class ReportStore
{
    private readonly string? _reportsRoot;
    private readonly string? _careReportsRoot;

    internal ReportStore(string? reportsRoot = null, string? careReportsRoot = null)
    {
        _reportsRoot = reportsRoot;
        _careReportsRoot = careReportsRoot;
    }

    public string ReportsRoot => _reportsRoot ?? RuntimePaths.ReportsRoot;

    private string CareReportsRoot => _careReportsRoot ?? CareAuditPaths.ReportsRoot;

    public IReadOnlyList<ReportSummary> LoadRecentReports()
    {
        return EnumerateReportDirectories()
            .Select(ToSummary)
            .Where(summary => summary is not null)
            .Cast<ReportSummary>()
            .OrderByDescending(summary => GetLastWriteUtc(summary))
            .Take(30)
            .ToArray();
    }

    private IEnumerable<string> EnumerateReportDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { ReportsRoot, CareReportsRoot }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                if (HasReportArtifact(directory) && seen.Add(directory))
                {
                    yield return directory;
                }
            }
        }
    }

    private static bool HasReportArtifact(string directory) =>
        new[] { "report.json", "report.html", "summary.txt", "scan_report.json", "scan_report.html", "session_report.json", "session_report.html" }
            .Any(file => File.Exists(Path.Combine(directory, file)));

    private static ReportSummary? ToSummary(string directory)
    {
        try
        {
            if (File.Exists(Path.Combine(directory, "scan_report.json")))
            {
                return ReadSystemCareSummary(directory);
            }

            if (File.Exists(Path.Combine(directory, "session_report.json")))
            {
                return ReadUnifiedCareSummary(directory);
            }

            return ReadEngineSummary(directory);
        }
        catch
        {
            // A malformed report must never hide all other reports or break the history page.
            return null;
        }
    }

    private static ReportSummary? ReadEngineSummary(string directory)
    {
        var html = Path.Combine(directory, "report.html");
        var json = Path.Combine(directory, "report.json");
        var text = Path.Combine(directory, "summary.txt");
        if (!File.Exists(html) && !File.Exists(json) && !File.Exists(text))
        {
            return null;
        }

        var title = Path.GetFileName(directory);
        var module = GuessModuleFromFolder(directory);
        var status = "unknown";
        var summary = "";
        var created = FormatFileTime(directory);
        var actionsTakenCount = 0;
        int? healthScore = null;

        if (File.Exists(json))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            var root = doc.RootElement;
            title = ReadString(root, "title") ?? title;
            module = ReadString(root, "module") ?? module;
            status = ReadString(root, "status") ?? status;
            summary = ReadString(root, "summary") ?? summary;
            created = FormatCreatedAt(ReadString(root, "createdAt")) ?? created;
            healthScore = ReadInt(root, "healthScore") ?? ReadNestedInt(root, "intelligence", "score");
            if (root.TryGetProperty("actionsTaken", out var taken) && taken.ValueKind == JsonValueKind.Array)
            {
                actionsTakenCount = taken.GetArrayLength();
            }
        }

        return CreateSummary(title, module, status, summary, created, actionsTakenCount, html, json, text, healthScore);
    }

    private static ReportSummary ReadSystemCareSummary(string directory)
    {
        var json = Path.Combine(directory, "scan_report.json");
        var html = Path.Combine(directory, "scan_report.html");
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        var root = doc.RootElement;
        var title = "시스템 케어 보고서";
        var module = ReadString(root, "moduleTitle") ?? "시스템 케어";
        var healthScore = ReadInt(root, "healthScore");
        var grade = ReadString(root, "healthGrade") ?? "완료";
        var summary = ReadString(root, "summary") ?? "시스템 케어 검사가 완료되었습니다.";
        var actionsTaken = ReadApplyCount(directory);
        if (actionsTaken > 0)
        {
            summary += $" · 정리 {actionsTaken}개";
        }

        return CreateSummary(
            title,
            module,
            grade,
            summary,
            FormatFileTime(directory),
            actionsTaken,
            html,
            json,
            "",
            healthScore);
    }

    private static ReportSummary ReadUnifiedCareSummary(string directory)
    {
        var json = Path.Combine(directory, "session_report.json");
        var html = Path.Combine(directory, "session_report.html");
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        var root = doc.RootElement;
        var completed = ReadBool(root, "completed");
        var scoreAfter = ReadInt(root, "scoreAfter");
        var scoreBefore = ReadInt(root, "scoreBefore");
        var scope = ReadString(root, "scope") ?? "full";
        var summary = ReadString(root, "summary") ?? "통합 점검이 완료되었습니다.";
        if (scoreBefore.HasValue && scoreAfter.HasValue && !summary.Contains("점수", StringComparison.Ordinal))
        {
            summary += $" · 점수 {scoreBefore}→{scoreAfter}";
        }

        return CreateSummary(
            "PC 점검·복구 보고서",
            TranslateModule(scope),
            completed ? "completed" : "incomplete",
            summary,
            FormatCreatedAt(ReadString(root, "completedAt")) ?? FormatFileTime(directory),
            ReadInt(root, "stepCount") ?? 0,
            html,
            json,
            "",
            scoreAfter ?? scoreBefore);
    }

    private static ReportSummary CreateSummary(
        string title,
        string module,
        string status,
        string summary,
        string created,
        int actionsTakenCount,
        string html,
        string json,
        string text,
        int? healthScore) =>
        new(
            title,
            TranslateModule(module),
            status,
            TranslateStatus(status),
            string.IsNullOrWhiteSpace(summary) ? "요약 없음" : summary,
            created,
            actionsTakenCount,
            html,
            json,
            text,
            healthScore);

    private static int ReadApplyCount(string directory)
    {
        var path = Path.Combine(directory, "apply_report.json");
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return ReadInt(doc.RootElement, "appliedCount") ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime GetLastWriteUtc(ReportSummary summary)
    {
        foreach (var path in new[] { summary.JsonPath, summary.ReportPath, summary.SummaryPath })
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return File.GetLastWriteTimeUtc(path);
            }
        }

        return DateTime.MinValue;
    }

    private static string GuessModuleFromFolder(string directory)
    {
        var name = Path.GetFileName(directory);
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[^1] : "점검";
    }

    private static string FormatFileTime(string directory) =>
        Directory.GetLastWriteTime(directory).ToString("yyyy-MM-dd HH:mm");

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

        return DateTimeOffset.TryParse(raw, out var parsed)
            ? parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
            : raw;
    }

    private static string? ReadString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;

    private static int? ReadNestedInt(JsonElement root, string parent, string property) =>
        root.TryGetProperty(parent, out var parentElement) && parentElement.ValueKind == JsonValueKind.Object
            ? ReadInt(parentElement, property)
            : null;

    private static bool ReadBool(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static string TranslateModule(string module) => module.ToLowerInvariant() switch
    {
        "quick" => "빠른 점검",
        "system" => "시스템",
        "driver" => "드라이버",
        "audio" => "오디오",
        "full" => "전체 점검",
        _ => module
    };

    public static string TranslateStatus(string status)
    {
        var value = status.ToLowerInvariant();
        if (value is "completed" or "complete" or "완료")
        {
            return "완료";
        }

        if (value is "incomplete" or "cancelled" or "canceled")
        {
            return "일부 완료";
        }

        if (value.Contains("ok") || value.Contains("양호") || value.Contains("정상") || value.Contains("healthy") || value.Contains("우수"))
        {
            return "양호";
        }

        if (value.Contains("critical") || value.Contains("위험") || value.Contains("fail") || value.Contains("error"))
        {
            return "위험";
        }

        if (value.Contains("warn") || value.Contains("주의") || value.Contains("caution") || value.Contains("보통"))
        {
            return "주의";
        }

        return status;
    }
}