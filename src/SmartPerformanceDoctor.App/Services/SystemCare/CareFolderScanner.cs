namespace SmartPerformanceDoctor.App.Services.SystemCare;

/// <summary>Fast bounded folder measurement — parallel scan, reparse skip, size extrapolation when capped.</summary>
public static class CareFolderScanner
{
    public const int DefaultMaxFiles = 3500;
    private const int CountOnlyBudget = 6000;

    public sealed record FolderScanResult(
        long TotalBytes,
        int FileCount,
        bool Estimated,
        string Note);

    public static FolderScanResult Measure(
        string path,
        CancellationToken cancellationToken,
        int maxDepth = 3,
        int maxFiles = DefaultMaxFiles,
        bool tempFolder = false,
        bool topLevelOnly = false)
    {
        if (!Directory.Exists(path))
        {
            return new FolderScanResult(0, 0, false, "missing");
        }

        if (topLevelOnly)
        {
            maxDepth = 0;
        }
        else if (tempFolder)
        {
            maxDepth = Math.Min(maxDepth, 2);
            maxFiles = Math.Min(maxFiles, 1800);
        }

        var ctx = new ScanContext(maxFiles, maxDepth, cancellationToken);
        ScanDirectory(path, 0, ctx);

        if (ctx.Truncated && ctx.SizedFiles > 0 && ctx.ExtraPathsSeen > 0)
        {
            var avg = (double)ctx.TotalBytes / ctx.SizedFiles;
            var estimated = (long)(ctx.TotalBytes + ctx.ExtraPathsSeen * avg);
            return new FolderScanResult(
                estimated,
                ctx.SizedFiles + ctx.ExtraPathsSeen,
                true,
                "estimated");
        }

        var note = ctx.Truncated ? "file_cap" : "complete";
        return new FolderScanResult(ctx.TotalBytes, ctx.SizedFiles, false, note);
    }

    private sealed class ScanContext
    {
        private int _sizedFiles;
        private int _extraPaths;
        private long _totalBytes;

        public ScanContext(int maxFiles, int maxDepth, CancellationToken cancellationToken)
        {
            MaxFiles = maxFiles;
            MaxDepth = maxDepth;
            CancellationToken = cancellationToken;
        }

        public int MaxFiles { get; }
        public int MaxDepth { get; }
        public CancellationToken CancellationToken { get; }
        public bool Truncated { get; set; }
        public bool CountOnly { get; set; }
        public bool Exhausted { get; set; }

        public long TotalBytes => _totalBytes;
        public int SizedFiles => _sizedFiles;
        public int ExtraPathsSeen => _extraPaths;

        public bool TryAddSizedFile(long bytes)
        {
            if (CountOnly || _sizedFiles >= MaxFiles)
            {
                Truncated = true;
                CountOnly = true;
                return false;
            }

            Interlocked.Add(ref _totalBytes, bytes);
            Interlocked.Increment(ref _sizedFiles);
            return true;
        }

        public bool TryAddCountOnly()
        {
            if (!CountOnly)
            {
                Truncated = true;
                CountOnly = true;
            }

            if (_extraPaths >= CountOnlyBudget)
            {
                Exhausted = true;
                return false;
            }

            Interlocked.Increment(ref _extraPaths);
            return true;
        }
    }

    private static void ScanDirectory(string directory, int depth, ScanContext ctx)
    {
        if (ctx.Exhausted)
        {
            return;
        }

        ctx.CancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            if (ctx.Exhausted)
            {
                return;
            }

            ctx.CancellationToken.ThrowIfCancellationRequested();
            if (ctx.CountOnly)
            {
                if (!ctx.TryAddCountOnly())
                {
                    return;
                }

                continue;
            }

            try
            {
                if (!ctx.TryAddSizedFile(new FileInfo(file).Length))
                {
                    if (!ctx.TryAddCountOnly())
                    {
                        return;
                    }
                }
            }
            catch
            {
                // Skip locked/inaccessible files.
            }
        }

        if (depth >= ctx.MaxDepth || ctx.Exhausted)
        {
            return;
        }

        string[] subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(directory)
                .Where(d => !IsReparsePoint(d))
                .ToArray();
        }
        catch
        {
            return;
        }

        if (subdirs.Length == 0)
        {
            return;
        }

        if (depth < 1 && subdirs.Length > 1 && !ctx.CountOnly)
        {
            Parallel.ForEach(
                subdirs,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(4, subdirs.Length),
                    CancellationToken = ctx.CancellationToken
                },
                sub => ScanDirectory(sub, depth + 1, ctx));
        }
        else
        {
            foreach (var sub in subdirs)
            {
                if (ctx.Exhausted)
                {
                    return;
                }

                ScanDirectory(sub, depth + 1, ctx);
            }
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }
}