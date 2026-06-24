namespace SmartPerformanceDoctor.Aegis;

public static class AegisPostInstall
{
    public static AegisInstallStatus FinalizeInstall(string installRoot, string version)
    {
        AegisRuntimeContext.SetInstallRoot(installRoot);
        AegisBaselineService.RebuildBaseline(installRoot, version);
        AegisAclHardening.ApplyMirrorRootAcls();

        string? offlinePack = null;
        try
        {
            offlinePack = AegisOfflineCapsule.ExportLatestPack();
        }
        catch
        {
            // Optional export.
        }

        var serviceExe = AegisServiceInstaller.ResolveServiceExePath(installRoot);
        var serviceResult = AegisServiceInstaller.Install(serviceExe, installRoot, version);
        return new AegisInstallStatus
        {
            BaselineReady = File.Exists(AegisMirrorPaths.ManifestFile),
            CapsuleReady = File.Exists(AegisMirrorPaths.CapsuleFile),
            BackupSlotReady = AegisSlotManager.BackupSlotReady,
            OfflinePackPath = offlinePack,
            TpmAvailable = AegisKeyProtector.IsTpmAvailable(),
            KeyProtectionMode = AegisRecoveryCapsule.ReadKeyProtectionMode() ?? "dpapi-localmachine",
            RecoveryServiceInstalled = serviceResult.Success,
            RecoveryServiceMessage = serviceResult.Message,
            ProtectionLevel = ComputeLevel(serviceResult.Success)
        };
    }

    public static AegisInstallStatus FinalizeRepair(string installRoot, string version) =>
        FinalizeInstall(installRoot, version);

    public static void FinalizeUninstall()
    {
        _ = AegisServiceInstaller.Uninstall();
        if (Directory.Exists(AegisMirrorPaths.Root))
        {
            try
            {
                Directory.Delete(AegisMirrorPaths.Root, recursive: true);
            }
            catch
            {
                // Best-effort.
            }
        }

        var config = AegisMirrorPaths.ServiceConfigFile;
        if (File.Exists(config))
        {
            try
            {
                File.Delete(config);
            }
            catch
            {
                // Best-effort.
            }
        }
    }

    private static int ComputeLevel(bool serviceInstalled)
    {
        var offline = AegisOfflineCapsule.LatestOfflinePackPath() is not null;
        var backup = AegisSlotManager.BackupSlotReady;
        var service = AegisServiceInstaller.GetStatus();
        if (serviceInstalled && service.Running && offline && backup && !AegisMirrorPaths.UsingUserFallback)
        {
            return 5;
        }

        if (serviceInstalled && service.Running)
        {
            return 4;
        }

        return backup ? 3 : 2;
    }
}

public sealed class AegisInstallStatus
{
    public bool BaselineReady { get; init; }
    public bool CapsuleReady { get; init; }
    public bool BackupSlotReady { get; init; }
    public string? OfflinePackPath { get; init; }
    public bool TpmAvailable { get; init; }
    public string KeyProtectionMode { get; init; } = "";
    public bool RecoveryServiceInstalled { get; init; }
    public string RecoveryServiceMessage { get; init; } = "";
    public int ProtectionLevel { get; init; }
}