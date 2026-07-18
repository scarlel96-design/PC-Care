using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.App.Services.Update;

namespace SmartPerformanceDoctor.App.Services;

public static class CareAuditPaths
{
    public static string ReportsRoot => ResolveWritableRoot("reports");

    public static string UnifiedCareSessionsRoot => Path.Combine(ReportsRoot, "unified_care", "sessions");

    public static string SystemCareRoot => Path.Combine(ReportsRoot, "system_care");

    public static string JobHistoryRoot => Path.Combine(ReportsRoot, "job_history");

    public static string CreateUnifiedCareSessionFolder(string sessionId)
    {
        var folder = Path.Combine(UnifiedCareSessionsRoot, sessionId);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string CreateSystemCareFolder(CareScanMode mode)
    {
        var label = mode == CareScanMode.Smart ? "smart" : "precision";
        var folder = Path.Combine(SystemCareRoot, label, DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(folder);
        AppendJobHistory("system_care", folder, $"{label} scan");
        return folder;
    }

    public static void AppendJobHistory(string category, string folder, string summary)
    {
        try
        {
            Directory.CreateDirectory(JobHistoryRoot);
            var entry = new
            {
                at = DateTimeOffset.Now.ToString("o"),
                category,
                folder,
                summary
            };
            File.AppendAllText(
                Path.Combine(JobHistoryRoot, "index.jsonl"),
                System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch
        {
            // Best-effort index.
        }
    }

    private static string ResolveWritableRoot(string segment)
    {
        var installCandidate = Path.Combine(UpdatePaths.AppInstallDirectory, segment);
        if (TryEnsureDirectory(installCandidate))
        {
            return installCandidate;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var productRoot = new[]
        {
            Path.Combine(localAppData, AstraCareBranding.LocalAppDataFolder),
            Path.Combine(localAppData, "AstraCare"),
            Path.Combine(localAppData, "SmartPerformanceDoctor")
        }.FirstOrDefault(Directory.Exists) ?? Path.Combine(localAppData, AstraCareBranding.LocalAppDataFolder);

        var fallback = Path.Combine(productRoot, segment);
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool TryEnsureDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".write_probe_{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}