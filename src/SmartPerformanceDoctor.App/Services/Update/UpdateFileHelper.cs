namespace SmartPerformanceDoctor.App.Services.Update;

internal static class UpdateFileHelper
{
    private static readonly HashSet<string> HotExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".winmd"
    };

    public static bool IsHotFile(string relativePath) =>
        HotExtensions.Contains(Path.GetExtension(relativePath));

    public static bool IsTargetWritable(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return true;
        }

        try
        {
            using var stream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryCopy(string source, string target)
    {
        try
        {
            var dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(source, target, true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static int CopyTree(string sourceRoot, string targetRoot)
    {
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var target = Path.Combine(targetRoot, relative);
            if (TryCopy(file, target))
            {
                copied++;
            }
        }

        return copied;
    }
}