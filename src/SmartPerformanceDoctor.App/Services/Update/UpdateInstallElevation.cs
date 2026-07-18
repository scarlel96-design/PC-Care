using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Update;

/// <summary>
/// Detects whether in-app updates can write into the current install directory.
/// Program Files installs require elevation; without it restart-apply silently fails
/// (seen across 50.0.0–50.4.0: 1097 copy Access Denied → version stuck).
/// </summary>
internal static class UpdateInstallElevation
{
    public static bool IsAdministrator() => ProcessElevationService.IsAdministrator();

    /// <summary>
    /// True when the process cannot write a probe file into the install directory.
    /// </summary>
    public static bool RequiresElevation(string? installDirectory = null)
    {
        if (IsAdministrator())
        {
            return false;
        }

        var dir = string.IsNullOrWhiteSpace(installDirectory)
            ? UpdatePaths.AppInstallDirectory
            : installDirectory;

        if (string.IsNullOrWhiteSpace(dir))
        {
            return false;
        }

        try
        {
            if (!Directory.Exists(dir))
            {
                // Creating under Program Files also needs elevation.
                var parent = Directory.GetParent(dir);
                if (parent is null || !parent.Exists)
                {
                    return LooksLikeProtectedProgramFiles(dir);
                }

                dir = parent.FullName;
            }

            var probe = Path.Combine(dir, $".pccare_write_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            // Locked/IO issue is not elevation; treat protected PF paths as elevation.
            return LooksLikeProtectedProgramFiles(dir);
        }
        catch
        {
            return LooksLikeProtectedProgramFiles(dir);
        }
    }

    public static bool LooksLikeProtectedProgramFiles(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return StartsWithPath(full, pf) || StartsWithPath(full, pf86);
        }
        catch
        {
            return false;
        }
    }

    private static bool StartsWithPath(string full, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               || string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }
}
