using System.IO;
using SmartPerformanceDoctor.Aegis;

namespace SmartPerformanceDoctor.Setup;

internal static class InstallerStaging
{
    public static void StageUninstallerArtifacts(string installRoot)
    {
        var setupPath = ResolveRunningSetupPath();
        if (string.IsNullOrWhiteSpace(setupPath))
        {
            return;
        }

        var programData = InstallerPaths.ProgramDataRoot;
        var installerDir = Path.Combine(programData, "Installer");
        Directory.CreateDirectory(installerDir);

        var setupFileName = $"PCCare_Setup_v{InstallerPaths.ProductVersion}.exe";
        var stagedSetup = Path.Combine(installerDir, setupFileName);
        File.Copy(setupPath, stagedSetup, overwrite: true);

        var localUninstallDir = Path.Combine(installRoot, "Uninstall");
        Directory.CreateDirectory(localUninstallDir);
        var localSetupCopy = Path.Combine(localUninstallDir, setupFileName);
        File.Copy(setupPath, localSetupCopy, overwrite: true);

        var uninstallCmdProgramData = Path.Combine(installerDir, "PCCare_Uninstall.cmd");
        WriteUninstallCmd(uninstallCmdProgramData, stagedSetup);

        var uninstallCmdLocal = Path.Combine(installRoot, "PCCare_Uninstall.cmd");
        WriteUninstallCmd(uninstallCmdLocal, localSetupCopy);

        var readme = Path.Combine(installRoot, "UNINSTALL.txt");
        File.WriteAllText(
            readme,
            "PC 케어 프로 제거`r`n`r`n" +
            $"1) 시작 메뉴 또는 바탕화면 -> 「{InstallerPaths.ProductName} 제거」`r`n" +
            $"2) 설치 폴더 -> PCCare_Uninstall.cmd 더블클릭`r`n" +
            $"3) 설치 폴더\\Uninstall\\{setupFileName} 실행 후 제거`r`n" +
            $"4) {uninstallCmdProgramData}`r`n`r`n" +
            "※ 일반 제거는 확인 문구 입력이 필요 없습니다. 금고 데이터까지 지울 때만 「금고삭제」를 입력하세요.`r`n");
    }

    public static string? ResolveStagedUninstallerPath()
    {
        foreach (var candidate in ResolveUninstallerCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> ResolveUninstallerCandidates()
    {
        var versionedName = $"PCCare_Setup_v{InstallerPaths.ProductVersion}.exe";
        var installRoot = AppExecutableResolver.DefaultInstallDirectory();
        yield return Path.Combine(installRoot, "Uninstall", versionedName);
        yield return Path.Combine(
            InstallerPaths.ProgramDataRoot,
            "Installer",
            versionedName);
    }

    private static void WriteUninstallCmd(string cmdPath, string setupExe)
    {
        File.WriteAllText(
            cmdPath,
            "@echo off`r`n" +
            $"start \"\" \"{setupExe}\" --uninstall`r`n");
    }

    private static string? ResolveRunningSetupPath()
    {
        foreach (var candidate in new[] { Environment.ProcessPath, Environment.GetCommandLineArgs().FirstOrDefault() })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                var full = Path.GetFullPath(candidate);
                if (File.Exists(full) && full.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return full;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
