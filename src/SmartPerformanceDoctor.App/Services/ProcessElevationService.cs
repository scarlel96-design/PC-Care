using System.Diagnostics;
using System.Security.Principal;

namespace SmartPerformanceDoctor.App.Services;

public static class ProcessElevationService
{
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryRelaunchAsAdministrator()
    {
        if (IsAdministrator())
        {
            return false;
        }

        var exePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = BuildRelaunchArguments()
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        var baseDir = AppContext.BaseDirectory;
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

    private static string BuildRelaunchArguments()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length <= 1)
        {
            return "";
        }

        return string.Join(" ", args.Skip(1).Select(QuoteIfNeeded));
    }

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}