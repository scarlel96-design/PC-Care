using SmartPerformanceDoctor.App.Branding;

namespace SmartPerformanceDoctor.App.Services;

public static class RuntimePaths
{
    public static string InstallRoot =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string EngineDirectory => Path.Combine(InstallRoot, "engine");

    public static string ContentDirectory => Path.Combine(InstallRoot, "content");

    public static string RulesDirectory => ResolveContentPath("rules");

    public static string AssetsDirectory => ResolveContentPath("assets");

    public static string CommercialDataDirectory => ResolveContentPath("data", "commercial");

    public static string ResolveCoreEnginePath() =>
        ResolveEngineWithAliases(AstraCareBranding.EngineExe, AstraCareBranding.LegacyCoreExe);

    public static string ResolveRepairHelperPath() =>
        ResolveEngineWithAliases(AstraCareBranding.RepairHelperExe, AstraCareBranding.LegacyRepairHelperExe);

    public static string ResolveAegisRecoveryHelperPath() =>
        ResolveEngineWithAliases(AstraCareBranding.AegisRecoveryHelperExe, AstraCareBranding.LegacyAegisRecoveryHelperExe);

    public static string ResolveEnginePath(string fileName) => ResolveEngineWithAliases(fileName);

    private static string ResolveEngineWithAliases(params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var preferred = Path.Combine(EngineDirectory, fileName);
            if (File.Exists(preferred))
            {
                return preferred;
            }

            var legacy = Path.Combine(InstallRoot, fileName);
            if (File.Exists(legacy))
            {
                return legacy;
            }
        }

        return Path.Combine(EngineDirectory, fileNames[0]);
    }

    public static string ResolveContentPath(params string[] segments)
    {
        var preferred = Path.Combine(new[] { ContentDirectory }.Concat(segments).ToArray());
        if (Directory.Exists(preferred) || File.Exists(preferred))
        {
            return preferred;
        }

        var legacy = Path.Combine(new[] { InstallRoot }.Concat(segments).ToArray());
        return Directory.Exists(legacy) || File.Exists(legacy) ? legacy : preferred;
    }

    public static string UserRoot
    {
        get
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var candidates = new[]
            {
                Path.Combine(desktop, AstraCareBranding.UserDataFolder),
                Path.Combine(desktop, AstraCareBranding.LegacyUserDataFolder),
                Path.Combine(desktop, AstraCareBranding.LegacyUserDataFolder2)
            };
            return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }
    }

    public static string ReportsRoot => Path.Combine(UserRoot, "Reports");
    public static string RepairLogsRoot => Path.Combine(UserRoot, "RepairLogs");
    public static string ErrorBundlesRoot => Path.Combine(UserRoot, "ErrorBundles");
    public static string CrashLogsRoot => Path.Combine(UserRoot, "CrashLogs");
    public static string FirstRunMarker => Path.Combine(UserRoot, ".first_run_complete");

    public static void EnsureUserFolders()
    {
        Directory.CreateDirectory(UserRoot);
        Directory.CreateDirectory(ReportsRoot);
        Directory.CreateDirectory(RepairLogsRoot);
        Directory.CreateDirectory(ErrorBundlesRoot);
        Directory.CreateDirectory(CrashLogsRoot);
    }
}