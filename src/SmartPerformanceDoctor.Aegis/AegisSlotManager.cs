namespace SmartPerformanceDoctor.Aegis;

public static class AegisSlotManager
{
    public static void EnsureLayout()
    {
        Directory.CreateDirectory(AegisMirrorPaths.ActiveSlotDirectory);
        Directory.CreateDirectory(AegisMirrorPaths.BackupSlotDirectory);
    }

    public static void SnapshotActiveToBackup(AegisRecoveryManifest manifest)
    {
        EnsureLayout();
        MirrorDirectory(AegisMirrorPaths.LastKnownGoodDirectory, AegisMirrorPaths.BackupSlotDirectory);
        WriteSlotMarker(AegisMirrorPaths.BackupSlotDirectory, manifest.Version, "backup");
        MirrorDirectory(AegisMirrorPaths.LastKnownGoodDirectory, AegisMirrorPaths.ActiveSlotDirectory);
        WriteSlotMarker(AegisMirrorPaths.ActiveSlotDirectory, manifest.Version, "active");
    }

    public static int RestoreFromBackupSlot(AegisRecoveryManifest manifest)
    {
        if (!Directory.Exists(AegisMirrorPaths.BackupSlotDirectory)
            || !Directory.EnumerateFiles(AegisMirrorPaths.BackupSlotDirectory, "*", SearchOption.AllDirectories).Any())
        {
            return 0;
        }

        MirrorDirectory(AegisMirrorPaths.BackupSlotDirectory, AegisMirrorPaths.LastKnownGoodDirectory);
        var verifier = new AegisIntegrityVerifier();
        var findings = verifier.VerifyAgainstManifest(manifest);
        return verifier.TryRestoreFromLastKnownGood(findings);
    }

    public static bool BackupSlotReady =>
        Directory.Exists(AegisMirrorPaths.BackupSlotDirectory)
        && Directory.EnumerateFiles(AegisMirrorPaths.BackupSlotDirectory, "*", SearchOption.AllDirectories).Any();

    private static void MirrorDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, true);
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var dest = Path.Combine(destination, relative);
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void WriteSlotMarker(string slotDirectory, string version, string slotName)
    {
        var marker = Path.Combine(slotDirectory, ".slot.json");
        File.WriteAllText(marker, $$"""
            {
              "slot": "{{slotName}}",
              "version": "{{version}}",
              "updatedAt": "{{DateTimeOffset.Now:o}}"
            }
            """);
    }
}