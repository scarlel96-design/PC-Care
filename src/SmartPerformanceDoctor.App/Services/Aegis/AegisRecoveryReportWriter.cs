using System.Text.Json;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Branding;

namespace SmartPerformanceDoctor.App.Services.Aegis;

internal static class AegisRecoveryReportWriter
{
    public static string Write(AegisMirrorStatus status, string operationId)
    {
        RuntimePaths.EnsureUserFolders();
        var reportsDir = Path.Combine(RuntimePaths.UserRoot, "AegisReports");
        Directory.CreateDirectory(reportsDir);
        var basePath = Path.Combine(reportsDir, operationId);

        var payload = new
        {
            product = AstraCareBranding.ProductFormal,
            operationId,
            status.Message,
            status.ProtectionLevel,
            status.ProtectedFileCount,
            status.IntegrityFailures,
            status.RepairedFiles,
            status.RepairAttempted,
            manifestReady = status.ManifestReady,
            capsuleReady = status.CapsuleReady,
            lastCheckAt = status.LastCheckAt,
            lastRepairAt = status.LastRepairAt,
            findings = status.Findings,
            completedAt = DateTimeOffset.Now
        };

        var jsonPath = $"{basePath}.json";
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        var findingsHtml = status.Findings.Count == 0
            ? "<li>이상 없음</li>"
            : string.Join("", status.Findings.Select(f => $"<li>{System.Net.WebUtility.HtmlEncode(f)}</li>"));

        File.WriteAllText(
            $"{basePath}.html",
            $"""
            <html><head><meta charset="utf-8"/><title>복구 미러 보고서</title></head>
            <body style="font-family:Segoe UI,sans-serif;padding:24px">
            <h1>복구 미러 복구 보고서</h1>
            <p><b>작업 ID:</b> {operationId}</p>
            <p><b>상태:</b> {System.Net.WebUtility.HtmlEncode(status.Message)}</p>
            <p><b>보호 등급:</b> Level {status.ProtectionLevel}</p>
            <p><b>복구:</b> {status.RepairedFiles}건 · <b>남은 이슈:</b> {status.IntegrityFailures}건</p>
            <ul>{findingsHtml}</ul>
            <p style="color:#666">{System.Net.WebUtility.HtmlEncode(AstraCareBranding.AegisDisclaimer)}</p>
            </body></html>
            """);

        return jsonPath;
    }
}