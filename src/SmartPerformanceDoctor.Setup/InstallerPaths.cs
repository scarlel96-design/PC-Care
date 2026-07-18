using System.IO;
using System.Reflection;

namespace SmartPerformanceDoctor.Setup;

internal static class InstallerPaths
{
    public static string ProductVersion { get; } = ResolveProductVersion();
    public const string ProductName = "PC 케어 프로";

    private static string ResolveProductVersion()
    {
        var informational = typeof(InstallerPaths).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? informational[..plus] : informational;
        }

        return typeof(InstallerPaths).Assembly.GetName().Version?.ToString(3) ?? "50.1.1";
    }

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
            EmbeddedInstallerPayload.LayoutDirectory,
            Path.Combine(AppContext.BaseDirectory, "layout"),
            Path.Combine(AppContext.BaseDirectory, "..", "layout"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "installer", "layout"))
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static string? ResolveMsiPath()
    {
        var versionToken = ProductVersion.Replace('.', '_');
        var msiName = $"SmartPerformanceDoctor_v{versionToken}.msi";
        var candidates = new[]
        {
            Path.Combine(EmbeddedInstallerPayload.CacheRoot, msiName),
            Path.Combine(AppContext.BaseDirectory, msiName),
            Path.Combine(AppContext.BaseDirectory, "..", msiName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "installer", msiName))
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}