using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public static class CommercialReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void WriteArtifacts(string reportDir, JsonObject report, IReadOnlyList<string>? matchedRuleIds = null)
    {
        if (string.IsNullOrWhiteSpace(reportDir) || !Directory.Exists(reportDir))
        {
            return;
        }

        try
        {
            var evidence = new JsonObject
            {
                ["reportId"] = report["id"]?.ToString() ?? Path.GetFileName(reportDir),
                ["createdAt"] = report["createdAt"]?.ToString() ?? DateTimeOffset.Now.ToString("o"),
                ["before"] = report["scanFindings"]?.DeepClone(),
                ["after"] = report["actionsTaken"]?.DeepClone(),
                ["matchedRules"] = new JsonArray(matchedRuleIds?.Select(x => JsonValue.Create(x)).ToArray() ?? Array.Empty<JsonNode?>())
            };
            File.WriteAllText(Path.Combine(reportDir, "evidence.json"), evidence.ToJsonString(JsonOptions), Encoding.UTF8);

            var timeline = new JsonArray();
            if (report["events"] is JsonArray events)
            {
                foreach (var ev in events)
                {
                    timeline.Add(new JsonObject
                    {
                        ["at"] = report["createdAt"]?.ToString() ?? "",
                        ["event"] = ev?.ToString() ?? ""
                    });
                }
            }

            if (timeline.Count == 0)
            {
                timeline.Add(new JsonObject
                {
                    ["at"] = report["createdAt"]?.ToString() ?? "",
                    ["event"] = report["summary"]?.ToString() ?? "scan completed"
                });
            }

            File.WriteAllText(Path.Combine(reportDir, "timeline.json"), timeline.ToJsonString(JsonOptions), Encoding.UTF8);
            File.WriteAllText(Path.Combine(reportDir, "report.expert.html"), RenderExpertHtml(report, matchedRuleIds), Encoding.UTF8);
        }
        catch
        {
            // Report enrichment must not break care sessions.
        }
    }

    private static string RenderExpertHtml(JsonObject report, IReadOnlyList<string>? matchedRuleIds)
    {
        static string Esc(string? v) => System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        var rules = matchedRuleIds is { Count: > 0 }
            ? string.Join("<br/>", matchedRuleIds.Select(Esc))
            : "(매칭 규칙 없음)";

        return $@"<!doctype html>
<html lang=""ko"">
<head><meta charset=""utf-8""><title>전문가 보고서</title>
<style>body{{font-family:Consolas,""Segoe UI"",monospace;background:#111;color:#def;padding:24px}} .card{{border:1px solid #345;padding:16px;margin:12px 0}}</style>
</head><body>
<h1>Expert Diagnostics</h1>
<p>모듈: {Esc(report["module"]?.ToString())} · 상태: {Esc(report["status"]?.ToString())}</p>
<div class=""card""><h2>Rule Matching</h2><p>{rules}</p></div>
<div class=""card""><h2>Signals / Findings</h2><pre>{Esc(report["scanFindings"]?.ToJsonString())}</pre></div>
<div class=""card""><h2>Protocol Decision</h2><pre>{Esc(report["recommendedActions"]?.ToJsonString() ?? report["actionsTaken"]?.ToJsonString())}</pre></div>
<div class=""card""><h2>Confidence / Root Causes</h2><pre>{Esc(report["rootCauses"]?.ToJsonString())}</pre></div>
</body></html>";
    }
}