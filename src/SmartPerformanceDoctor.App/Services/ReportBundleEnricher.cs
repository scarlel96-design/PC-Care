using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Services;

public static class ReportBundleEnricher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void AppendActionsTaken(string reportDir, IReadOnlyList<string> actionsTaken, bool includeRepair)
    {
        if (string.IsNullOrWhiteSpace(reportDir) || !Directory.Exists(reportDir))
        {
            return;
        }

        var jsonPath = Path.Combine(reportDir, "report.json");
        if (!File.Exists(jsonPath))
        {
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(jsonPath)) as JsonObject;
            if (root is null)
            {
                return;
            }

            var taken = new JsonArray();
            foreach (var action in actionsTaken)
            {
                if (!string.IsNullOrWhiteSpace(action))
                {
                    taken.Add(action);
                }
            }

            if (taken.Count == 0)
            {
                taken.Add(includeRepair
                    ? "복구 단계가 계획되었으나 실행된 항목이 없습니다."
                    : "진단 스캔만 수행 · PC 설정 변경 없음");
            }

            root["actionsTaken"] = taken;
            if (includeRepair && taken.Count > 0)
            {
                root["summary"] = $"점검·복구 완료 · 조치 {taken.Count}건 기록";
            }

            File.WriteAllText(jsonPath, root.ToJsonString(JsonOptions), Encoding.UTF8);
            File.WriteAllText(Path.Combine(reportDir, "report.html"), RenderHtml(root), Encoding.UTF8);
            File.WriteAllText(Path.Combine(reportDir, "summary.txt"), RenderText(root), Encoding.UTF8);
            CommercialReportWriter.WriteArtifacts(reportDir, root);
        }
        catch
        {
            // Broken report bundles must not break care sessions.
        }
    }

    private static string RenderHtml(JsonObject report)
    {
        static string Esc(string? v) => System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        static string Items(JsonArray? arr)
        {
            if (arr is null || arr.Count == 0)
            {
                return "<li>해당 없음</li>";
            }

            return string.Join('\n', arr.Select(x => $"<li>{Esc(x?.ToString())}</li>"));
        }

        var title = report["title"]?.ToString() ?? "점검 보고서";
        var created = report["createdAt"]?.ToString() ?? "";
        var module = report["module"]?.ToString() ?? "";
        var status = report["status"]?.ToString() ?? "";
        var summary = report["summary"]?.ToString() ?? "";
        var findings = Items(report["scanFindings"] as JsonArray);
        var roots = Items(report["rootCauses"] as JsonArray);
        var taken = Items(report["actionsTaken"] as JsonArray);
        var events = Items(report["events"] as JsonArray);

        return $@"<!doctype html>
<html lang=""ko"">
<head><meta charset=""utf-8""><title>{Esc(title)}</title>
<style>
body {{ font-family: ""Segoe UI"", ""Malgun Gothic"", sans-serif; background:#0b1020; color:#eef3ff; margin:0; padding:28px; }}
.card {{ background:#151d31; border:1px solid #2b385a; border-radius:18px; padding:20px; margin:14px 0; }}
.muted {{ color:#9ca8bc; }}
</style></head>
<body>
<h1>{Esc(title)}</h1>
<p class=""muted"">생성: {Esc(created)} · 모듈: {Esc(module)}</p>
<div class=""card""><h2>상태 요약</h2><p>{Esc(status)}</p><p>{Esc(summary)}</p></div>
<div class=""card""><h2>정밀 스캔 결과</h2><ul>{findings}</ul></div>
<div class=""card""><h2>원인 후보</h2><ul>{roots}</ul></div>
<div class=""card""><h2>조치 사항</h2><ul>{taken}</ul></div>
<div class=""card""><h2>이벤트 기록</h2><ul>{events}</ul></div>
</body></html>";
    }

    private static string RenderText(JsonObject report)
    {
        static string Lines(JsonArray? arr) =>
            arr is null || arr.Count == 0
                ? "- 해당 없음"
                : string.Join('\n', arr.Select(x => $"- {x}"));

        return $"""
            {report["title"]}
            {report["createdAt"]}
            모듈: {report["module"]}

            상태: {report["status"]}
            요약: {report["summary"]}

            정밀 스캔 결과:
            {Lines(report["scanFindings"] as JsonArray)}

            원인 후보:
            {Lines(report["rootCauses"] as JsonArray)}

            조치 사항:
            {Lines(report["actionsTaken"] as JsonArray)}

            이벤트:
            {Lines(report["events"] as JsonArray)}
            """;
    }
}