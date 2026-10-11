using System.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>Publish authenticated plaintext without replacing an existing destination.</summary>
public static class VaultExportWriter
{
    public static void ValidateAncestors(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("VAULT-REPARSE-POINT");
        }
    }

    public static void WriteNew(string target, byte[] content, string expectedHash, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        target = Path.GetFullPath(target);
        ValidateAncestors(target);
        if (File.Exists(target) || Directory.Exists(target)) throw new IOException("VAULT-EXPORT-COLLISION");
        var directory = Path.GetDirectoryName(target)!;
        using var parent = VaultFileBoundary.PinDirectory(directory, create: true);
        var temporary = Path.Combine(directory, ".pccare-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        using var file = VaultFileBoundary.OpenWrite(directory, Path.GetFileName(temporary), create: true, deletable: true);
        var published = false;
        try
        {
            var stream = file.Stream;
            {
                for (var offset = 0; offset < content.Length; offset += 128 * 1024)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Write(content, offset, Math.Min(128 * 1024, content.Length - offset));
                }
                stream.Flush(flushToDisk: true);
                stream.Position = 0;
                var actualHash = Convert.ToHexString(SHA256.HashData(stream));
                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("VAULT-EXPORT-HASH");
            }
            cancellationToken.ThrowIfCancellationRequested();
            parent.Validate();
            file.Verify(content.LongLength, expectedHash);
            file.RenameOpenedFile(target);
            published = true;
        }
        finally
        {
            // Remove only this operation's temporary file. Preserve fixed-name leftovers.
            if (!published) file.DeleteOpenedFile();
        }
    }
}
