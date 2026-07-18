namespace SmartPerformanceDoctor.App.Services.SystemCare;

/// <summary>Bounded .lnk enumeration — depth and count caps keep desktop/start-menu scans fast.</summary>
internal static class CareShortcutScanner
{
    public const int DefaultMaxLinks = 600;
    public const int DefaultMaxDepth = 4;

    public static IEnumerable<string> Enumerate(
        IEnumerable<string> roots,
        CancellationToken cancellationToken,
        int maxLinks = DefaultMaxLinks,
        int maxDepth = DefaultMaxDepth)
    {
        var found = 0;
        foreach (var root in roots.Distinct())
        {
            if (found >= maxLinks || !Directory.Exists(root))
            {
                continue;
            }

            var queue = new Queue<(string Dir, int Depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0 && found < maxLinks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (dir, depth) = queue.Dequeue();

                IEnumerable<string> links;
                try
                {
                    links = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var link in links)
                {
                    if (found >= maxLinks)
                    {
                        yield break;
                    }

                    found++;
                    yield return link;
                }

                if (depth >= maxDepth)
                {
                    continue;
                }

                IEnumerable<string> subdirs;
                try
                {
                    subdirs = Directory.EnumerateDirectories(dir);
                }
                catch
                {
                    continue;
                }

                foreach (var sub in subdirs)
                {
                    queue.Enqueue((sub, depth + 1));
                }
            }
        }
    }
}