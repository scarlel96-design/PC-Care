using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class AppSelfCheckService
{
    public IReadOnlyList<AppDiagnosticItem> Run()
    {
        var baseDir = RuntimePaths.InstallRoot;
        var items = new List<AppDiagnosticItem>
        {
            CheckFile("AstraCore", RuntimePaths.ResolveCoreEnginePath()),
            CheckFile("AstraRepairHelper", RuntimePaths.ResolveRepairHelperPath()),
            CheckDirectory("Rules", RuntimePaths.RulesDirectory),
            CheckDirectory("Assets", RuntimePaths.AssetsDirectory),
            CheckDirectory("Reports Root", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "Reports"), createIfMissing: true),
            CheckDirectory("Repair Logs Root", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "RepairLogs"), createIfMissing: true)
        };

        return items;
    }

    private static AppDiagnosticItem CheckFile(string name, string path)
    {
        var exists = File.Exists(path);
        return new AppDiagnosticItem(
            name,
            exists ? "OK" : "MISSING",
            path,
            exists ? "파일이 존재합니다." : "파일이 없습니다. publish/copy-native-engines 단계를 확인하세요.");
    }

    private static AppDiagnosticItem CheckDirectory(string name, string path, bool createIfMissing = false)
    {
        if (!Directory.Exists(path) && createIfMissing)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch
            {
                // ignored
            }
        }

        var exists = Directory.Exists(path);
        return new AppDiagnosticItem(
            name,
            exists ? "OK" : "MISSING",
            path,
            exists ? "폴더가 존재합니다." : "폴더가 없습니다.");
    }
}
