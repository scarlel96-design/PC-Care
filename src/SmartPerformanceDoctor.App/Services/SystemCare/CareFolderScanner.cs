namespace SmartPerformanceDoctor.App.Services.SystemCare;

/// <summary>Bound folder measurement work. Skip reparse points and report capped estimates.</summary>
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
        private int _directories;
        public bool TryEnterDirectory()
        {
            if(++_directories>Math.Max(64,MaxFiles)){Truncated=true;Exhausted=true;return false;}
            return true;
        }

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

        // Enumerate lazily. Bound directory work without queuing nested thread-pool scans.
        try
        {
            foreach(var sub in Directory.EnumerateDirectories(directory))
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                if(ctx.Exhausted)return;
                if(!ctx.TryEnterDirectory())return;
                if(IsReparsePoint(sub))continue;
                ScanDirectory(sub,depth+1,ctx);
            }
        }
        catch(UnauthorizedAccessException){ctx.Truncated=true;}
        catch(IOException){ctx.Truncated=true;}
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
            return true;
        }
    }
}