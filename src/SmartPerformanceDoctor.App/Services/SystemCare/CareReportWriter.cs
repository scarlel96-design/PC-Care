using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

internal static class CareReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void WriteScanReports(string auditFolder, CareScanResult result)
    {
        Directory.CreateDirectory(auditFolder);
        var jsonPath = Path.Combine(auditFolder, "scan_report.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions), Encoding.UTF8);

        var findingsHtml = result.Findings.Count == 0
            ? "<li>특이 항목 없음</li>"
            : string.Join("", result.Findings.Select(f =>
                "<li><b>[" + System.Net.WebUtility.HtmlEncode(f.RiskLabel) + "]</b> " +
                System.Net.WebUtility.HtmlEncode(f.Title) + " — " + System.Net.WebUtility.HtmlEncode(f.Detail) + "</li>"));

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><meta charset=\"utf-8\"/><title>시스템 케어 검사 보고서</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,sans-serif;padding:24px;max-width:960px}");
        html.AppendLine("h1{margin-top:0} .meta{color:#555} ul{line-height:1.6}</style></head><body>");
        html.AppendLine("<h1>시스템 케어 검사 보고서</h1>");
        html.Append("<p class=\"meta\"><b>모드:</b> ").Append(System.Net.WebUtility.HtmlEncode(result.ModuleTitle)).AppendLine("</p>");
        html.Append("<p class=\"meta\"><b>요약:</b> ").Append(System.Net.WebUtility.HtmlEncode(result.Summary)).AppendLine("</p>");
        html.Append("<p class=\"meta\"><b>건강 점수:</b> ").Append(result.HealthScore).Append("점 (")
            .Append(System.Net.WebUtility.HtmlEncode(result.HealthGrade)).AppendLine(")</p>");
        html.Append("<p class=\"meta\"><b>감사 체인:</b> ").Append(result.AuditChainValid ? "정상" : "주의").AppendLine("</p>");
        html.Append("<p class=\"meta\"><b>기록 폴더:</b> ").Append(System.Net.WebUtility.HtmlEncode(auditFolder)).AppendLine("</p>");
        html.AppendLine("<h2>발견 항목</h2><ul>" + findingsHtml + "</ul>");
        html.Append("<p class=\"meta\">생성: ").Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine("</p>");
        html.AppendLine("</body></html>");
        File.WriteAllText(Path.Combine(auditFolder, "scan_report.html"), html.ToString(), Encoding.UTF8);
        CareAuditPaths.AppendJobHistory("system_care", auditFolder, result.Summary);
    }

    public static void WriteApplyReport(string auditFolder, CareApplyResult result, bool includeReview)
    {
        var jsonPath = Path.Combine(auditFolder, "apply_report.json");
        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(new { result.AppliedCount, result.SkippedCount, includeReview, result.Message }, JsonOptions),
            Encoding.UTF8);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>시스템 케어 적용 보고서</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,sans-serif;padding:24px}</style></head><body>");
        html.AppendLine("<h1>시스템 케어 적용 보고서</h1>");
        html.Append("<p><b>적용:</b> ").Append(result.AppliedCount).Append("개 · <b>건너뜀:</b> ")
            .Append(result.SkippedCount).AppendLine("개</p>");
        html.Append("<p><b>확인 필요 포함:</b> ").Append(includeReview ? "예" : "아니오").AppendLine("</p>");
        html.Append("<p>").Append(System.Net.WebUtility.HtmlEncode(result.Message)).AppendLine("</p>");
        html.Append("<p>기록 폴더: ").Append(System.Net.WebUtility.HtmlEncode(auditFolder)).AppendLine("</p>");
        html.Append("<p>생성: ").Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine("</p>");
        html.AppendLine("</body></html>");
        File.WriteAllText(Path.Combine(auditFolder, "apply_report.html"), html.ToString(), Encoding.UTF8);
    }
}