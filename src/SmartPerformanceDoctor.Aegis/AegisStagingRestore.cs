using System.Security.Cryptography;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisStagingRestore
{
    public static string CreateOperationDirectory()
    {
        AegisMirrorPaths.EnsureLayout();
        var operationId = $"aegis-repair-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var dir = Path.Combine(AegisMirrorPaths.StagingDirectory, operationId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static bool VerifyStagingHashes(string stagingDirectory, AegisRecoveryManifest manifest)
    {
        foreach (var entry in manifest.Files)
        {
            var relative = entry.Path.Replace('/', Path.DirectorySeparatorChar);
            var staged = Path.Combine(stagingDirectory, relative);
            if (!File.Exists(staged))
            {
                return false;
            }

            if (new FileInfo(staged).Length != entry.Size)
            {
                return false;
            }

            using var stream = File.OpenRead(staged);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static int CommitStaging(string stagingDirectory, AegisRecoveryManifest manifest)
    {
        var restored = 0;
        foreach (var entry in manifest.Files)
        {
            var relative = entry.Path.Replace('/', Path.DirectorySeparatorChar);
            var staged = Path.Combine(stagingDirectory, relative);
            var target = Path.Combine(AegisRuntimeContext.InstallRoot, relative);
            if (!File.Exists(staged))
            {
                continue;
            }

            using (var stagedStream = File.OpenRead(staged))
            {
                var stagedHash = Convert.ToHexString(SHA256.HashData(stagedStream)).ToLowerInvariant();
                if (!string.Equals(stagedHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var needsCommit = true;
            if (File.Exists(target))
            {
                using var activeStream = File.OpenRead(target);
                var activeHash = Convert.ToHexString(SHA256.HashData(activeStream)).ToLowerInvariant();
                needsCommit = !string.Equals(activeHash, entry.Sha256, StringComparison.OrdinalIgnoreCase);
            }

            if (!needsCommit)
            {
                continue;
            }

            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(target))
            {
                Quarantine(target, "staging-commit");
            }

            File.Copy(staged, target, overwrite: true);
            restored++;
        }

        return restored;
    }

    private static void Quarantine(string targetPath, string reason)
    {
        AegisMirrorPaths.EnsureLayout();
        var name = $"{DateTimeOffset.Now:yyyyMMddHHmmss}_{Path.GetFileName(targetPath)}_{Sanitize(reason)}.quarantine";
        var quarantinePath = Path.Combine(AegisMirrorPaths.QuarantineDirectory, name);
        try
        {
            File.Move(targetPath, quarantinePath, overwrite: true);
        }
        catch
        {
            // Best-effort.
        }
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim('_');
}