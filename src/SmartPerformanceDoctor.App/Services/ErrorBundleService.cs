using System.IO.Compression;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class ErrorBundleService
{
    public string BundleRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "ErrorBundles");

    public IReadOnlyList<ErrorBundleItem> InspectSources()
    {
        var desktopRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor");
        var baseDir = RuntimePaths.InstallRoot;

        return
        [
            Inspect("App Folder", baseDir),
            Inspect("Reports", Path.Combine(desktopRoot, "Reports")),
            Inspect("Repair Logs", Path.Combine(desktopRoot, "RepairLogs")),
            Inspect("Error Bundles", BundleRoot),
            Inspect("Core Engine", RuntimePaths.ResolveEnginePath("smart_performance_doctor_core.exe")),
            Inspect("Repair Helper", RuntimePaths.ResolveEnginePath("smart_performance_doctor_repair_helper.exe"))
        ];
    }

    public string CreateBundle()
    {
        Directory.CreateDirectory(BundleRoot);

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var stage = Path.Combine(BundleRoot, "stage_" + stamp);
        var zip = Path.Combine(BundleRoot, $"SmartPerformanceDoctor_ErrorBundle_{stamp}.zip");

        if (Directory.Exists(stage))
        {
            Directory.Delete(stage, recursive: true);
        }

        Directory.CreateDirectory(stage);

        CopyIfExists(AppContext.BaseDirectory, Path.Combine(stage, "app"), recursive: false);

        var desktopRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor");
        CopyIfExists(Path.Combine(desktopRoot, "Reports"), Path.Combine(stage, "Reports"), recursive: true);
        CopyIfExists(Path.Combine(desktopRoot, "RepairLogs"), Path.Combine(stage, "RepairLogs"), recursive: true);

        File.WriteAllText(Path.Combine(stage, "ENVIRONMENT.txt"), BuildEnvironmentText());

        if (File.Exists(zip))
        {
            File.Delete(zip);
        }

        ZipFile.CreateFromDirectory(stage, zip);
        Directory.Delete(stage, recursive: true);

        return zip;
    }

    private static ErrorBundleItem Inspect(string name, string path)
    {
        var exists = File.Exists(path) || Directory.Exists(path);
        return new ErrorBundleItem(
            name,
            path,
            exists ? "OK" : "MISSING",
            exists ? "수집 가능" : "대상이 없습니다.");
    }

    private static void CopyIfExists(string source, string destination, bool recursive)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            return;
        }

        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(destination);

        var files = Directory.GetFiles(source, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string BuildEnvironmentText()
    {
        return string.Join(Environment.NewLine,
            $"Timestamp: {DateTimeOffset.Now:o}",
            $"OS: {Environment.OSVersion}",
            $"MachineName: {Environment.MachineName}",
            $"UserName: {Environment.UserName}",
            $"Process64Bit: {Environment.Is64BitProcess}",
            $"OS64Bit: {Environment.Is64BitOperatingSystem}",
            $"BaseDirectory: {AppContext.BaseDirectory}");
    }
}
