namespace SmartPerformanceDoctor.Aegis;

public static class AegisBaselineService
{
    public static void RebuildBaseline(string installRoot, string version)
    {
        AegisRuntimeContext.SetInstallRoot(installRoot);
        AegisMirrorPaths.EnsureLayout();

        var verifier = new AegisIntegrityVerifier();
        var manifest = verifier.BuildBaselineManifest(version);
        verifier.SaveManifest(manifest);
        verifier.SnapshotLastKnownGood(manifest);
        AegisSlotManager.SnapshotActiveToBackup(manifest);
        var capsuleHash = AegisRecoveryCapsule.BuildFromLastKnownGood(manifest);
        manifest.CapsuleHash = capsuleHash;
        verifier.SaveManifest(manifest, capsuleHash);
        AegisAuditChain.Append(
            $"aegis-baseline-{DateTimeOffset.Now:yyyyMMddHHmmss}",
            "rebuild-baseline",
            installRoot,
            "success");

        try
        {
            AegisOfflineCapsule.ExportLatestPack();
        }
        catch
        {
            // Offline pack is optional during baseline rebuild.
        }

        AegisAclHardening.ApplyMirrorRootAcls();
    }
}