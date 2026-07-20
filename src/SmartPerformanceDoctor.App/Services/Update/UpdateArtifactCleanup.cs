namespace SmartPerformanceDoctor.App.Services.Update;

public sealed record UpdateArtifactCleanupResult(
    bool PackageDeleted,
    bool StagingDeleted,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Removes only PCCare-managed update artifacts after the installed version has
/// been verified. User-selected packages outside the managed inbox are preserved.
/// </summary>
public static class UpdateArtifactCleanup
{
    public static UpdateArtifactCleanupResult TryCleanupCompletedUpdate(
        string? packagePath,
        string? stagingDirectory) =>
        TryCleanupCompletedUpdate(packagePath, stagingDirectory, UpdatePaths.Inbox, UpdatePaths.Staging);

    internal static UpdateArtifactCleanupResult TryCleanupCompletedUpdate(
        string? packagePath,
        string? stagingDirectory,
        string inboxRoot,
        string stagingRoot)
    {
        var warnings = new List<string>();
        var packageDeleted = false;
        var stagingDeleted = false;

        if (!string.IsNullOrWhiteSpace(packagePath)
            && IsManagedDescendant(packagePath, inboxRoot)
            && IsUpdatePackage(packagePath))
        {
            try
            {
                if (File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                }

                packageDeleted = !File.Exists(packagePath);
            }
            catch (Exception ex)
            {
                warnings.Add($"업데이트 패키지 삭제 실패: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(stagingDirectory)
            && IsManagedDescendant(stagingDirectory, stagingRoot))
        {
            try
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }

                stagingDeleted = !Directory.Exists(stagingDirectory);
            }
            catch (Exception ex)
            {
                warnings.Add($"업데이트 임시 폴더 삭제 실패: {ex.Message}");
            }
        }

        return new UpdateArtifactCleanupResult(packageDeleted, stagingDeleted, warnings);
    }

    internal static bool IsManagedDescendant(string candidate, string managedRoot)
    {
        try
        {
            var fullCandidate = Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(managedRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (fullCandidate.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fullCandidate.StartsWith(
                fullRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUpdatePackage(string path) =>
        Path.GetExtension(path) is var extension
        && (extension.Equals(".spdup", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase));
}