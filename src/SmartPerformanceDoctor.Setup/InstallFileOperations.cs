using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using SmartPerformanceDoctor.Aegis;

namespace SmartPerformanceDoctor.Setup;

internal static class InstallFileOperations
{
    private static readonly string[] KnownProcessNames =
    [
        "PCCare",
        "SmartPerformanceDoctor",
        "AstraCare",
        "smart_performance_doctor_core",
        "smart_performance_doctor_repair_helper",
        "AegisRecoveryService",
        "AegisRecoveryHelper"
    ];

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

    public static bool RequiresElevation(string targetDir)
    {
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            return false;
        }

        var full = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var root in GetProtectedRoots())
        {
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void PrepareTargetDirectory(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        TryStopRecoveryServices();
        StopProcessesUsingDirectory(targetDir);
    }

    public static void CopyFile(string source, string destination)
    {
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(destination))
                {
                    File.SetAttributes(destination, FileAttributes.Normal);
                    try
                    {
                        File.Delete(destination);
                    }
                    catch
                    {
                        // Fall through to overwrite via File.Copy.
                    }
                }

                File.Copy(source, destination, overwrite: true);
                File.SetAttributes(destination, FileAttributes.Normal);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                StopProcessesUsingDirectory(Path.GetDirectoryName(destination) ?? destination);
                Thread.Sleep(250 * attempt);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                StopProcessesUsingDirectory(Path.GetDirectoryName(destination) ?? destination);
                Thread.Sleep(250 * attempt);
            }
        }

        if (File.Exists(destination))
        {
            File.SetAttributes(destination, FileAttributes.Normal);
        }

        File.Copy(source, destination, overwrite: true);
        File.SetAttributes(destination, FileAttributes.Normal);
    }

    private static void TryStopRecoveryServices()
    {
        foreach (var serviceName in new[] { AegisProduct.RecoveryServiceName, AegisProduct.LegacyRecoveryServiceName })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("sc.exe", $"stop {serviceName}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(5000);
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private static IEnumerable<string> GetProtectedRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                roots.Add(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch
            {
                // Ignore invalid paths.
            }
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        return roots;
    }

    private static void StopProcessesUsingDirectory(string targetDir)
    {
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            return;
        }

        var normalizedTarget = Path.GetFullPath(targetDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!ShouldStopProcess(process, normalizedTarget))
                {
                    continue;
                }

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                    }
                }
                catch
                {
                    // Best effort.
                }
            }
        }

        Thread.Sleep(300);
    }

    private static bool ShouldStopProcess(Process process, string normalizedTarget)
    {
        if (KnownProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var modulePath = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(modulePath))
            {
                var normalizedModule = Path.GetFullPath(modulePath);
                if (normalizedModule.StartsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Access denied for system processes — ignore.
        }

        return false;
    }
}