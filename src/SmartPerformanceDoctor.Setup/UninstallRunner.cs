using System.IO;
using System.Text.Json;
using SmartPerformanceDoctor.Aegis;

namespace SmartPerformanceDoctor.Setup;

internal enum UninstallScope
{
    ProgramOnly,
    ProgramAndSettings,
    ProgramSettingsAndReports
}

internal sealed class UninstallRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<UninstallReport> UninstallAsync(
        string targetDir,
        UninstallScope scope,
        bool deleteVaultData,
        IProgress<(int percent, string detail)> progress)
    {
        await Task.Yield();
        progress.Report((10, "프로그램 파일 제거 중..."));

        if (Directory.Exists(targetDir))
        {
            TryDeleteDirectory(targetDir);
        }

        var programData = InstallerPaths.ProgramDataRoot;
        if (scope >= UninstallScope.ProgramAndSettings && Directory.Exists(programData))
        {
            progress.Report((40, "공용 설정 제거 중..."));
            RemoveProgramData(programData, scope, deleteVaultData);
        }

        if (scope >= UninstallScope.ProgramSettingsAndReports)
        {
            progress.Report((70, "보고서 폴더 정리 중..."));
            var desktopReports = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "AstraCare");
            if (!Directory.Exists(desktopReports))
            {
                desktopReports = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "SmartPerformanceDoctor");
            }
            if (Directory.Exists(desktopReports))
            {
                TryDeleteDirectory(desktopReports);
            }
        }

        if (!deleteVaultData)
        {
            progress.Report((85, "Secure Vault 사용자 데이터는 보존되었습니다."));
        }

        progress.Report((90, "복구 미러 제거 중..."));
        AegisPostInstall.FinalizeUninstall();
        MsiHelper.TryUninstallMsi();
        progress.Report((100, "제거 완료"));

        var report = new UninstallReport
        {
            TargetDirectory = targetDir,
            Scope = scope.ToString(),
            VaultDataRemoved = deleteVaultData,
            CompletedAt = DateTimeOffset.Now.ToString("o"),
            Success = true
        };
        WriteUninstallLog(report);
        return report;
    }

    private static void RemoveProgramData(string programData, UninstallScope scope, bool deleteVaultData)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(programData))
        {
            var name = Path.GetFileName(entry);
            if (name.Equals("InstallerLogs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || entry.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                || entry.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(entry);
                continue;
            }

            if (Directory.Exists(entry))
            {
                TryDeleteDirectory(entry);
            }
        }

        if (deleteVaultData)
        {
            var vaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartPerformanceDoctor",
                "secure_vault");
            if (Directory.Exists(vaultRoot))
            {
                TryDeleteDirectory(vaultRoot);
            }
        }
    }

    private static void WriteUninstallLog(UninstallReport report)
    {
        var logDir = Path.Combine(InstallerPaths.ProgramDataRoot, "InstallerLogs");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(Path.Combine(logDir, "uninstall_log.txt"), JsonSerializer.Serialize(report, JsonOptions));
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, true);
        }
        catch
        {
            // best effort
        }
    }
}

internal sealed record UninstallReport
{
    public string TargetDirectory { get; init; } = "";
    public string Scope { get; init; } = "";
    public bool VaultDataRemoved { get; init; }
    public string CompletedAt { get; init; } = "";
    public bool Success { get; init; }
}