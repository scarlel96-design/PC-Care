using System.Diagnostics;
using System.IO;

namespace SmartPerformanceDoctor.Setup;

internal static class MsiHelper
{
    public static void TryInstallMsi(string? msiPath)
    {
        if (string.IsNullOrWhiteSpace(msiPath) || !File.Exists(msiPath))
        {
            return;
        }

        RunMsiexec($"/i \"{msiPath}\" /qn /norestart");
    }

    public static void TryRepairMsi()
    {
        var msi = InstallerPaths.ResolveMsiPath();
        if (msi is null)
        {
            return;
        }

        RunMsiexec($"/fvecmus \"{msi}\" /qn /norestart");
    }

    public static void TryUninstallMsi()
    {
        var msi = InstallerPaths.ResolveMsiPath();
        if (msi is null)
        {
            return;
        }

        RunMsiexec($"/x \"{msi}\" /qn /norestart");
    }

    private static void RunMsiexec(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("msiexec.exe", arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            });
            process?.WaitForExit(120_000);
        }
        catch
        {
            // MSI is optional when layout copy succeeds
        }
    }
}