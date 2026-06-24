using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class SelfHealingService
{
    public IReadOnlyList<SelfHealingItem> InspectAndRepair()
    {
        RuntimePaths.EnsureUserFolders();

        var results = new List<SelfHealingItem>
        {
            EnsureDirectory("Reports", RuntimePaths.ReportsRoot, "medium"),
            EnsureDirectory("RepairLogs", RuntimePaths.RepairLogsRoot, "medium"),
            EnsureDirectory("ErrorBundles", RuntimePaths.ErrorBundlesRoot, "medium"),
            EnsureDirectory("CrashLogs", RuntimePaths.CrashLogsRoot, "medium"),
            CheckNativeEngine("Core Engine", "smart_performance_doctor_core.exe"),
            CheckNativeEngine("Repair Helper", "smart_performance_doctor_repair_helper.exe"),
            CheckDirectory("rules", RuntimePaths.RulesDirectory),
            CheckDirectory("assets", RuntimePaths.AssetsDirectory),
            CheckDirectory("docs", Path.Combine(RuntimePaths.InstallRoot, "docs"))
        };

        return results;
    }

    private static SelfHealingItem EnsureDirectory(string name, string path, string severity)
    {
        try
        {
            Directory.CreateDirectory(path);
            return new SelfHealingItem(name, "OK", severity, "폴더 확인/복구 완료", path);
        }
        catch (Exception ex)
        {
            return new SelfHealingItem(name, "FAIL", severity, $"폴더 복구 실패: {ex.Message}", path);
        }
    }

    private static SelfHealingItem CheckNativeEngine(string name, string fileName)
    {
        var path = RuntimePaths.ResolveEnginePath(fileName);
        if (File.Exists(path))
        {
            return new SelfHealingItem(name, "OK", "critical", "native engine 확인", path);
        }

        return new SelfHealingItem(
            name,
            "MISSING",
            "critical",
            "native engine 누락. scripts\\copy-native-engines.ps1 또는 portable layout repair가 필요합니다.",
            path);
    }

    private static SelfHealingItem CheckDirectory(string name, string path)
    {
        var exists = Directory.Exists(path);
        return new SelfHealingItem(name, exists ? "OK" : "MISSING", "medium", exists ? "폴더 확인" : "폴더 누락", path);
    }
}
