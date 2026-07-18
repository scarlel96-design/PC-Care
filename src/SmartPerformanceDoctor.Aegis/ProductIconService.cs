namespace SmartPerformanceDoctor.Aegis;

public static class ProductIconService
{
    public const string IconFileName = "SmartPerformanceDoctor.ico";

    public static string? ResolveIconPath(string? installRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(installRoot)
            ? AppContext.BaseDirectory
            : installRoot;

        foreach (var candidate in GetIconCandidates(root))
        {
            if (File.Exists(candidate) && candidate.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    public static IEnumerable<string> GetIconCandidates(string root)
    {
        yield return Path.Combine(root, "content", "assets", IconFileName);
        yield return Path.Combine(root, "assets", IconFileName);
        yield return Path.Combine(root, IconFileName);

        var layout = Path.Combine(root, "layout");
        if (Directory.Exists(layout))
        {
            yield return Path.Combine(layout, "content", "assets", IconFileName);
        }
    }
}