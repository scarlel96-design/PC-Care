namespace SmartPerformanceDoctor.App.Services;

public static class StartupDiagnostics
{
    private static readonly object Gate = new();

    public static void Write(string phase, string? detail = null)
    {
        try
        {
            RuntimePaths.EnsureUserFolders();
            var line = $"{DateTimeOffset.Now:O} [{phase}] {detail ?? ""}".TrimEnd();
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(RuntimePaths.UserRoot, "startup.log"), line + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never block launch.
        }
    }
}