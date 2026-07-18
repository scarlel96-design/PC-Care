namespace SmartPerformanceDoctor.AstraVault.Durable;

/// <summary>Test-only flush abstraction (FlushFileBuffers equivalent intent).</summary>
public static class Av3DurableFlush
{
    public static bool InjectNextFlushFailure { get; set; }

    public static bool TryFlush(Av3DurableWriteHandle handle)
    {
        if (InjectNextFlushFailure)
        {
            InjectNextFlushFailure = false;
            return false;
        }

        handle.Stream.Flush(flushToDisk: true);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var fs = new FileStream(handle.AbsolutePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                fs.Flush(flushToDisk: true);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }
}