namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Isolated temp storage for E-2 harness only (not user vault paths).</summary>
public sealed class Av3TestStorage : IDisposable
{
    public const string RootPrefix = "av3-e2-";

    public string RootPath { get; }

    private readonly HashSet<string> _flushed = new(StringComparer.OrdinalIgnoreCase);

    private Av3TestStorage(string rootPath)
    {
        RootPath = rootPath;
        Directory.CreateDirectory(RootPath);
    }

    public Av3TestStorage()
        : this(Path.Combine(Path.GetTempPath(), RootPrefix + Guid.NewGuid().ToString("N")))
    {
    }

    public static Av3TestStorage CreateIsolatedCleanupRoot()
        => new(Path.Combine(Path.GetTempPath(), RootPrefix + "cleanup-" + Guid.NewGuid().ToString("N")));

    public bool TruncateRelativePathOnNextFlush { get; set; }
    public bool SimulateDiskFullOnNextWrite { get; set; }
    public bool SimulateIoFailureOnNextWrite { get; set; }

    public void WritePending(string relativeName, ReadOnlySpan<byte> data)
    {
        ValidateRelativePath(relativeName);
        if (SimulateDiskFullOnNextWrite)
        {
            SimulateDiskFullOnNextWrite = false;
            throw new Av3SimulatedIoException(Av3DurabilitySimulationMode.SimulatedDiskFull);
        }

        if (SimulateIoFailureOnNextWrite)
        {
            SimulateIoFailureOnNextWrite = false;
            throw new Av3SimulatedIoException(Av3DurabilitySimulationMode.SimulatedExternalMediaRemoved);
        }

        var path = Path.Combine(RootPath, relativeName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data.ToArray());
    }

    public void MarkFlushed(string relativeName)
    {
        ValidateRelativePath(relativeName);
        if (TruncateRelativePathOnNextFlush)
        {
            TruncateRelativePathOnNextFlush = false;
            var path = Path.Combine(RootPath, relativeName);
            if (File.Exists(path))
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write);
                fs.SetLength(Math.Min(16, fs.Length));
            }
        }

        _flushed.Add(relativeName);
    }

    public bool IsFlushed(string relativeName) => _flushed.Contains(relativeName);

    public byte[]? TryReadFlushed(string relativeName)
    {
        ValidateRelativePath(relativeName);
        if (!_flushed.Contains(relativeName))
        {
            return null;
        }

        var path = Path.Combine(RootPath, relativeName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public static void ValidateRelativePath(string relativeName)
    {
        if (string.IsNullOrWhiteSpace(relativeName)
            || relativeName.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeName))
        {
            throw new InvalidOperationException("Harness relative path escape blocked.");
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // best-effort test cleanup
        }
    }
}