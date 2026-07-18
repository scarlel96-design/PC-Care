namespace SmartPerformanceDoctor.App.Services;

public static class InstalledAppPaths
{
    public static string ResolveClientExecutable()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var name in new[] { "PCCare.exe", "SmartPerformanceDoctor.exe", "AstraCare.exe" })
        {
            var candidate = Path.Combine(baseDir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return processPath ?? "";
    }
}