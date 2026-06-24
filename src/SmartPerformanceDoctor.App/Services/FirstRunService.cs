using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class FirstRunService
{
    public bool IsFirstRun => !File.Exists(RuntimePaths.FirstRunMarker);

    public IReadOnlyList<SelfHealingItem> RunSetup()
    {
        RuntimePaths.EnsureUserFolders();

        var results = new List<SelfHealingItem>
        {
            CheckOrCreateDirectory("사용자 루트", RuntimePaths.UserRoot, "critical"),
            CheckOrCreateDirectory("보고서 폴더", RuntimePaths.ReportsRoot, "medium"),
            CheckOrCreateDirectory("복구 로그 폴더", RuntimePaths.RepairLogsRoot, "medium"),
            CheckOrCreateDirectory("오류 번들 폴더", RuntimePaths.ErrorBundlesRoot, "medium"),
            CheckOrCreateDirectory("크래시 로그 폴더", RuntimePaths.CrashLogsRoot, "medium"),
            CheckFile("AstraCore", RuntimePaths.ResolveCoreEnginePath(), "critical"),
            CheckFile("AstraRepairHelper", RuntimePaths.ResolveRepairHelperPath(), "critical"),
            CheckDirectory("rules", RuntimePaths.RulesDirectory, "medium"),
            CheckDirectory("assets", RuntimePaths.AssetsDirectory, "medium")
        };

        File.WriteAllText(RuntimePaths.FirstRunMarker, DateTimeOffset.Now.ToString("o"));
        return results;
    }

    private static SelfHealingItem CheckOrCreateDirectory(string name, string path, string severity)
    {
        try
        {
            Directory.CreateDirectory(path);
            return new SelfHealingItem(name, "OK", severity, "폴더 준비 완료", path);
        }
        catch (Exception ex)
        {
            return new SelfHealingItem(name, "FAIL", severity, $"폴더 생성 실패: {ex.Message}", path);
        }
    }

    private static SelfHealingItem CheckFile(string name, string path, string severity)
    {
        var exists = File.Exists(path);
        return new SelfHealingItem(name, exists ? "OK" : "MISSING", severity, exists ? "파일 확인" : "필수 파일이 없습니다.", path);
    }

    private static SelfHealingItem CheckDirectory(string name, string path, string severity)
    {
        var exists = Directory.Exists(path);
        return new SelfHealingItem(name, exists ? "OK" : "MISSING", severity, exists ? "폴더 확인" : "필수 폴더가 없습니다.", path);
    }
}
