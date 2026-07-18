using System.IO;
using System.Text.Json;

namespace SmartPerformanceDoctor.Setup;

internal static class InstallRuntimeGuard
{
    private static readonly string[] RequiredRootRuntime =
    [
        "coreclr.dll",
        "hostfxr.dll",
        "hostpolicy.dll",
        "Microsoft.WindowsAppRuntime.Bootstrap.dll"
    ];

    private static readonly string[] BlockedDevAliases =
    [
        "SmartPerformanceDoctor.exe",
        "SmartPerformanceDoctor.runtimeconfig.json",
        "SmartPerformanceDoctor.deps.json",
        "SmartPerformanceDoctor.pri"
    ];

    public static void EnsureSelfContainedFromLayout(string layoutDir, string targetDir)
    {
        foreach (var name in RequiredRootRuntime)
        {
            var source = Path.Combine(layoutDir, name);
            if (!File.Exists(source))
            {
                throw new InvalidOperationException(
                    $"설치 레이아웃에 .NET/WinUI 런타임이 없습니다: {name}. " +
                    "self-contained publish 후 setup 을 다시 빌드하세요.");
            }

            InstallFileOperations.CopyFile(source, Path.Combine(targetDir, name));
        }

        var runtimeConfigSource = Path.Combine(layoutDir, "PCCare.runtimeconfig.json");
        if (!File.Exists(runtimeConfigSource) || !IsSelfContainedRuntimeConfig(runtimeConfigSource))
        {
            throw new InvalidOperationException(
                "PCCare.runtimeconfig.json 이 self-contained 형식이 아닙니다. 설치 패키지를 다시 빌드하세요.");
        }

        InstallFileOperations.CopyFile(runtimeConfigSource, Path.Combine(targetDir, "PCCare.runtimeconfig.json"));

        foreach (var blocked in BlockedDevAliases)
        {
            var path = Path.Combine(targetDir, blocked);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            catch
            {
                // Best effort.
            }
        }

        EnsureWinUiPriAlias(targetDir, layoutDir);
        EnsureWinUiThemeAssets(targetDir, layoutDir);
        EnsurePresentOrThrow(targetDir);
    }

    private static void EnsureWinUiThemeAssets(string targetDir, string layoutDir)
    {
        var relativeThemeFiles =
            new[] { "themeresources.xbf", "generic.xbf", "themeresources.xaml" };
        var themeSubdir = Path.Combine("Microsoft.UI.Xaml", "Themes");

        foreach (var root in new[] { layoutDir, targetDir })
        {
            var themeDir = Path.Combine(root, themeSubdir);
            if (!Directory.Exists(themeDir))
            {
                continue;
            }

            var destThemeDir = Path.Combine(targetDir, themeSubdir);
            Directory.CreateDirectory(destThemeDir);
            foreach (var name in relativeThemeFiles)
            {
                var source = Path.Combine(themeDir, name);
                if (!File.Exists(source))
                {
                    continue;
                }

                InstallFileOperations.CopyFile(source, Path.Combine(destThemeDir, name));
            }

            return;
        }

        throw new InvalidOperationException(
            "WinUI 테마 리소스(Microsoft.UI.Xaml\\Themes)가 설치 레이아웃에 없습니다. " +
            "scripts\\prepare-installer-layout.ps1 후 setup 을 다시 빌드하세요.");
    }

    private static void EnsureWinUiPriAlias(string targetDir, string layoutDir)
    {
        var mapPri = Path.Combine(targetDir, "Microsoft.UI.Xaml.pri");
        if (File.Exists(mapPri))
        {
            return;
        }

        foreach (var root in new[] { layoutDir, targetDir })
        {
            var controlsPri = Path.Combine(root, "Microsoft.UI.Xaml.Controls.pri");
            if (!File.Exists(controlsPri))
            {
                continue;
            }

            InstallFileOperations.CopyFile(controlsPri, mapPri);
            return;
        }

        throw new InvalidOperationException(
            "WinUI 리소스(PRI)가 설치본에 없습니다: Microsoft.UI.Xaml.Controls.pri. setup 을 다시 빌드하세요.");
    }

    public static void EnsurePresentOrThrow(string targetDir)
    {
        foreach (var name in RequiredRootRuntime)
        {
            var path = Path.Combine(targetDir, name);
            if (!File.Exists(path) || new FileInfo(path).Length < 1024)
            {
                throw new InvalidOperationException(
                    $"설치 후 런타임 파일이 누락되었습니다: {name}. " +
                    "%LocalAppData%\\PCCare\\installer-cache 를 삭제한 뒤 최신 PCCare_Setup_v*.exe 로 재설치하세요.");
            }
        }

        var runtimeConfig = Path.Combine(targetDir, "PCCare.runtimeconfig.json");
        if (!IsSelfContainedRuntimeConfig(runtimeConfig))
        {
            throw new InvalidOperationException(
                "설치된 PCCare.runtimeconfig.json 이 .NET 별도 설치 방식(framework-dependent)입니다. " +
                "최신 self-contained 설치본(~186MB)으로 다시 설치하세요. GitHub의 구형 ~80MB 파일은 사용하지 마세요.");
        }
    }

    private static bool IsSelfContainedRuntimeConfig(string runtimeConfigPath)
    {
        try
        {
            using var stream = File.OpenRead(runtimeConfigPath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("runtimeOptions", out var runtimeOptions))
            {
                return false;
            }

            if (runtimeOptions.TryGetProperty("framework", out _))
            {
                return false;
            }

            if (!runtimeOptions.TryGetProperty("includedFrameworks", out var includedFrameworks)
                || includedFrameworks.ValueKind != JsonValueKind.Array
                || includedFrameworks.GetArrayLength() == 0)
            {
                return false;
            }

            foreach (var framework in includedFrameworks.EnumerateArray())
            {
                if (framework.TryGetProperty("name", out var name)
                    && name.GetString() == "Microsoft.NETCore.App")
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}