using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class UnifiedCareSessionContext
{
    public string SessionId { get; init; } = "";
    public string AuditFolder { get; init; } = "";
    public string AuditLogPath { get; init; } = "";
}

public static class UnifiedCareAuditService
{
    public static UnifiedCareSessionContext BeginSession(CareRequest request)
    {
        var sessionId = $"care-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPerformanceDoctor",
            "unified_care",
            "sessions",
            sessionId);
        Directory.CreateDirectory(folder);

        var manifest = new
        {
            protocol = "unified-care-v2",
            sessionId,
            request.Scope,
            request.IncludeRepair,
            request.RiskAccepted,
            startedAt = DateTimeOffset.Now.ToString("o"),
            chain = "GENESIS"
        };
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        return new UnifiedCareSessionContext
        {
            SessionId = sessionId,
            AuditFolder = folder,
            AuditLogPath = Path.Combine(folder, "session_audit.log")
        };
    }

    public static void Append(UnifiedCareSessionContext ctx, string phase, string title, bool success)
    {
        var previous = ReadLastHash(ctx.AuditFolder);
        var line = $"{DateTimeOffset.Now:o}|{phase}|{title}|{(success ? "ok" : "fail")}|{previous}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
        File.AppendAllText(ctx.AuditLogPath, $"{line}|{hash}\n", Encoding.UTF8);
        UpdateChain(ctx.AuditFolder, hash);
    }

    public static void Complete(UnifiedCareSessionContext ctx, CareSessionResult result)
    {
        var reportPath = Path.Combine(ctx.AuditFolder, "session_report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ctx.SessionId,
            result.Scope,
            result.IncludeRepair,
            result.Completed,
            result.Summary,
            result.ScoreBefore,
            result.ScoreAfter,
            result.HealthDelta,
            result.AuditChainValid,
            stepCount = result.Steps.Count,
            completedAt = DateTimeOffset.Now.ToString("o")
        }, new JsonSerializerOptions { WriteIndented = true }));

        var htmlPath = Path.Combine(ctx.AuditFolder, "session_report.html");
        File.WriteAllText(htmlPath, $"""
            <html><head><meta charset="utf-8"/><title>PC 점검·복구 보고서</title></head>
            <body style="font-family:Segoe UI,sans-serif;padding:24px">
            <h1>PC 점검·복구 세션 (v2)</h1>
            <p><b>세션:</b> {ctx.SessionId}</p>
            <p><b>범위:</b> {result.Scope} · <b>복구:</b> {(result.IncludeRepair ? "포함" : "진단만")}</p>
            <p><b>점수:</b> {result.ScoreBefore?.ToString() ?? "-"} → {result.ScoreAfter?.ToString() ?? "-"} (Δ {result.HealthDelta?.ToString() ?? "-"})</p>
            <p><b>요약:</b> {result.Summary}</p>
            <p><b>감사 체인:</b> {(result.AuditChainValid ? "정상" : "주의")}</p>
            </body></html>
            """);
    }

    public static bool Verify(string auditFolder)
    {
        var logPath = Path.Combine(auditFolder, "session_audit.log");
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
            if (parts.Length < 6)
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
            return doc.RootElement.TryGetProperty("chain", out var chain)
                ? chain.GetString() ?? "GENESIS"
                : "GENESIS";
        }
        catch
        {
            return "GENESIS";
        }
    }

    private static void UpdateChain(string auditFolder, string hash)
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