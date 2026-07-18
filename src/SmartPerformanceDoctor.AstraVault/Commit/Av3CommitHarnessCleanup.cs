using System.Collections.Concurrent;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Idempotent harness cleanup marker (E-7).</summary>
internal static class Av3CommitHarnessCleanup
{
    private static readonly ConcurrentDictionary<string, byte> Completed = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryRunOnce(string vaultRoot, Action cleanup)
    {
        if (!Completed.TryAdd(vaultRoot, 0))
        {
            return false;
        }

        try
        {
            cleanup();
            return true;
        }
        catch
        {
            Completed.TryRemove(vaultRoot, out _);
            throw;
        }
    }

    public static void ResetHarness(string vaultRoot) => Completed.TryRemove(vaultRoot, out _);
}