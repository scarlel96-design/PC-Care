using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

namespace SmartPerformanceDoctor.App.Services;

/// <summary>Export supported vault data from an independent snapshot. Keep the source read-only.</summary>
public static class VaultSnapshotRecovery
{
    public static int Export(string sourceRoot, string outputRoot, string password)
    {
        sourceRoot = Path.GetFullPath(sourceRoot);
        outputRoot = Path.GetFullPath(outputRoot);
        if (outputRoot.StartsWith(sourceRoot.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(outputRoot) || File.Exists(outputRoot)) throw new IOException("Use a new recovery output directory.");
        using var sourcePin = WindowsFileBoundary.PinDirectory(sourceRoot);
        var files = EnumerateSource(sourceRoot).ToArray();
        if (files.Length > 10000) throw new IOException("Recovery snapshot file limit.");
        var leases = new List<(string Relative, WindowsFileBoundary.FileLease File)>();
        try
        {
            long total = 0;
            foreach (var path in files)
            {
                var relative = Path.GetRelativePath(sourceRoot, path);
                var file = WindowsFileBoundary.OpenRead(sourceRoot, relative);
                leases.Add((relative, file));
                total = checked(total + file.Stream.Length);
                if (total > 2L * 1024 * 1024 * 1024) throw new IOException("Recovery snapshot size limit.");
            }
            using var outputPin = WindowsFileBoundary.PinDirectory(outputRoot, create: true);
            var snapshot = Path.Combine(outputRoot, "snapshot");
            Directory.CreateDirectory(snapshot);
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in leases)
            {
                using var copy = WindowsFileBoundary.OpenWrite(snapshot, item.Relative, create: true);
                copy.CopyFrom(item.File);
                var hash = item.File.Hash();
                copy.Verify(item.File.Stream.Length, hash);
                hashes.Add(item.Relative, hash);
            }
            File.WriteAllText(Path.Combine(outputRoot, "source-hashes.json"), JsonSerializer.Serialize(hashes));
            using var vault = new LabVaultService(snapshot);
            var unlocked = vault.UnlockReadOnly(password);
            if (!unlocked.Success) throw new CryptographicException(unlocked.Message);
            var count = 0;
            foreach (var entry in vault.List())
            {
                // Separate entry directories prevent display-name collisions.
                var destination = Path.Combine(outputRoot, "export", entry.EntryId);
                var result = vault.ExportEntry(entry.EntryId, destination, stepUpConfirmed: true);
                if (!result.Success) throw new IOException(result.Message);
                count++;
            }
            foreach (var item in leases)
                if (item.File.Hash() != hashes[item.Relative]) throw new IOException("Source changed during recovery.");
            return count;
        }
        finally { foreach (var item in leases) item.File.Dispose(); }
    }

    private static IEnumerable<string> EnumerateSource(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        var directories = 0;
        var files = 0;
        while (pending.TryPop(out var directory))
        {
            if (++directories > 10000) throw new IOException("Recovery directory limit.");
            using var pin = WindowsFileBoundary.PinDirectory(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Recovery source contains a link.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                else
                {
                    if (++files > 10000) throw new IOException("Recovery file limit.");
                    yield return entry;
                }
            }
        }
    }
}
