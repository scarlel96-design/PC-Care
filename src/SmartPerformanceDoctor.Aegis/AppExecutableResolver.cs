namespace SmartPerformanceDoctor.Aegis;

public static class AppExecutableResolver
{
    public static string PreferredExecutable => AegisProduct.BrandedExePreferred;

    public static IReadOnlyList<string> MainExecutableCandidates { get; } =
    [
        AegisProduct.BrandedExePreferred,
        AegisProduct.MainExe,
        AegisProduct.BrandedExe
    ];

    public static string? ResolveMainExecutable(string installRoot)
    {
        foreach (var fileName in MainExecutableCandidates)
        {
            var path = Path.Combine(installRoot, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string DefaultInstallDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            AegisProduct.InstallFolderName);
}