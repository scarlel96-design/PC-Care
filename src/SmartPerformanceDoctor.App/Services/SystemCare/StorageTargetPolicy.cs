using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public static class StorageTargetPolicy
{
    // Evaluate every ancestor. A renamed cache inside a protected store remains protected.
    public static bool IsProtected(string path)
    {
        if (StorageProtectionRegistry.DefaultContains(path)) return true;
        var parts = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => part.Equals("vault", StringComparison.OrdinalIgnoreCase)
            || part.Equals("objects", StringComparison.OrdinalIgnoreCase)
            || part.Equals("generations", StringComparison.OrdinalIgnoreCase)
            || part.Equals("anchors", StringComparison.OrdinalIgnoreCase)
            || part.Equals("receipts", StringComparison.OrdinalIgnoreCase)
            || part.Equals("transactions", StringComparison.OrdinalIgnoreCase)
            || part.Equals("backups", StringComparison.OrdinalIgnoreCase)
            || part.Equals("recovery", StringComparison.OrdinalIgnoreCase)
            || part.Equals("PC-Care", StringComparison.OrdinalIgnoreCase)
            || part.Equals("AstraVault", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("vault.", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("recovery_slots.", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("state.dpapi", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("witness", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("journal", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("anchor", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("receipt", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(part).ToLowerInvariant() is ".av3" or ".av4" or ".av5" or ".avpack"
                or ".vault" or ".dpapi" or ".bak" or ".pfx" or ".pem" or ".key" or ".cer");
    }

    public static long? AllocatedBytes(SafeFileHandle handle)
    {
        return GetFileInformationByHandleEx(handle, 1, out var info, (uint)Marshal.SizeOf<StandardInfo>())
            && info.AllocationSize >= 0 ? info.AllocationSize : null;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StandardInfo
    {
        public long AllocationSize, EndOfFile;
        public uint NumberOfLinks;
        public byte DeletePending, Directory;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int type, out StandardInfo info, uint size);
}

public sealed record StorageScanPolicy(int MaximumDepth = 2, int MaximumEntries = 512, int MaximumMilliseconds = 5000)
{
    public void Validate()
    {
        if (MaximumDepth is < 0 or > 4 || MaximumEntries is < 1 or > 512
            || MaximumMilliseconds is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(StorageScanPolicy));
    }
}

public sealed record StorageSpaceAccounting(long LogicalBytes, long? AllocatedBytes, long CandidateBytes,
    long QuarantinedBytes, long RestorableBytes, long FinalDeletedBytes, long? VerifiedFreedBytes,
    long? UnattributedVolumeDelta)
{
    public static StorageSpaceAccounting Observe(long logical, long? allocated, long quarantined, long deleted,
        long? before, long? after) => new(logical, allocated, logical, quarantined, quarantined, deleted,
            deleted == 0 ? 0 : null, before.HasValue && after.HasValue ? after - before : null);
}
