namespace SmartPerformanceDoctor.Aegis;

public static class AegisRuntimeContext
{
    private static string? _overrideInstallRoot;

    public static string InstallRoot =>
        (_overrideInstallRoot ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static void SetInstallRoot(string installRoot)
    {
        _overrideInstallRoot = Path.GetFullPath(installRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static void ResetInstallRoot() => _overrideInstallRoot = null;

    public static string EngineDirectory => Path.Combine(InstallRoot, "engine");

    public static string ContentDirectory => Path.Combine(InstallRoot, "content");

    public static string RulesDirectory => ResolveContentPath("rules");

    public static string CommercialDataDirectory => ResolveContentPath("data", "commercial");

    public static string ResolveCoreEnginePath() =>
        ResolveEngineWithAliases(AegisProduct.EngineExe, AegisProduct.LegacyCoreExe);

    public static string ResolveRepairHelperPath() =>
        ResolveEngineWithAliases(AegisProduct.RepairHelperExe, AegisProduct.LegacyRepairHelperExe);

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
}