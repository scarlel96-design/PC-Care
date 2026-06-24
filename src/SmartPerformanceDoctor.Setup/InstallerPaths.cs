using System.IO;

namespace SmartPerformanceDoctor.Setup;

internal static class InstallerPaths
{
    public const string ProductVersion = "50.0.0";
    public const string ProductName = "PC 케어 프로";

    public static string ProgramDataRoot
    {
        get
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var candidates = new[]
            {
                Path.Combine(programData, "PCCare"),
                Path.Combine(programData, "AstraCare"),
                Path.Combine(programData, "SmartPerformanceDoctor")
            };

            foreach (var path in candidates)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return candidates[0];
        }
    }

    public static string? ResolveLayoutDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "layout"),
            Path.Combine(AppContext.BaseDirectory, "..", "layout"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "installer", "layout"))
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static string? ResolveMsiPath()
    {
        var versionToken = ProductVersion.Replace('.', '_');
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, $"SmartPerformanceDoctor_v{versionToken}.msi"),
            Path.Combine(AppContext.BaseDirectory, "..", $"SmartPerformanceDoctor_v{versionToken}.msi"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "installer", $"SmartPerformanceDoctor_v{versionToken}.msi"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}