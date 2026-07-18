using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.FaultInjection;

namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

/// <summary>Test-only 3-copy activation header write skeleton (not production writer).</summary>
public static class Av3HeaderCopyWriterHarness
{
    public static Av3HeaderCopyDurabilityState WriteThreeCopies(
        string vaultRoot,
        Av3HeaderCopyWritePlan plan,
        bool durableCopy0,
        bool durableCopy1,
        bool durableCopy2,
        byte[]? conflictingCopy2Bytes = null)
    {
        if (!vaultRoot.Contains("av3-e", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Header copy harness requires isolated av3-e* root.");
        }

        WriteCopy(vaultRoot, 0, plan.HeaderCopyBytes, durableCopy0);
        var copy1 = plan.HeaderCopyBytes.ToArray();
        if (copy1.Length > 32)
        {
            copy1[32] = 1;
        }

        WriteCopy(vaultRoot, 1, copy1, durableCopy1);
        var copy2 = conflictingCopy2Bytes ?? plan.HeaderCopyBytes;
        WriteCopy(vaultRoot, 2, copy2, durableCopy2);

        var manifest = Av3DurableManifest.Load(vaultRoot);
        manifest.HeaderCopyDurableCount = (durableCopy0 ? 1 : 0) + (durableCopy1 ? 1 : 0) + (durableCopy2 ? 1 : 0);
        manifest.HeaderCopyConflict = conflictingCopy2Bytes is not null;
        manifest.Save(vaultRoot);

        return new Av3HeaderCopyDurabilityState
        {
            Copy0Durable = durableCopy0,
            Copy1Durable = durableCopy1,
            Copy2Durable = durableCopy2,
            Copy0ConflictsWithCopy1 = false,
            Copy1ConflictsWithCopy2 = conflictingCopy2Bytes is not null,
            StaleCopyPresent = !durableCopy0 && (durableCopy1 || durableCopy2),
            UnauthenticatedHighGeneration = false
        };
    }

    private static void WriteCopy(string vaultRoot, byte index, byte[] bytes, bool durable)
    {
        var rel = Av3DurableFileLayout.HeaderCopyRelative(index);
        Av3TestStorage.ValidateRelativePath(rel);
        var path = Path.Combine(vaultRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        if (durable)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            fs.Flush(flushToDisk: true);
        }
    }
}