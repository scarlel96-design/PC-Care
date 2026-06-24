namespace SmartPerformanceDoctor.Aegis;

public static class AegisProtectionProvisioner
{
    public static AegisProvisionResult EnsureFullStack(string installRoot, string version)
    {
        AegisRuntimeContext.SetInstallRoot(installRoot);
        AegisMirrorPaths.ResetRootCache();

        var result = new AegisProvisionResult();
        if (!AegisMirrorPaths.EnsureLayout())
        {
            result.Message = "복구 미러 저장소를 준비하지 못했습니다.";
            return result;
        }

        var (manifest, signatureValid, _) = AegisManifestQuorum.TryLoadWithQuorum();
        if (manifest is null || !signatureValid)
        {
            AegisBaselineService.RebuildBaseline(installRoot, version);
            (manifest, signatureValid, _) = AegisManifestQuorum.TryLoadWithQuorum();
        }

        if (manifest is not null && !AegisSlotManager.BackupSlotReady)
        {
            AegisSlotManager.SnapshotActiveToBackup(manifest);
            result.BackupEnsured = true;
        }

        if (AegisOfflineCapsule.LatestOfflinePackPath() is null)
        {
            try
            {
                AegisOfflineCapsule.ExportLatestPack();
                result.OfflineEnsured = true;
            }
            catch
            {
                // Capsule may still be building.
            }
        }

        if (!AegisServiceInstaller.IsInstalled())
        {
            var serviceExe = AegisServiceInstaller.ResolveServiceExePath(installRoot);
            var install = AegisServiceInstaller.Install(serviceExe, installRoot, version);
            result.ServiceInstallAttempted = true;
            result.ServiceInstallMessage = install.Message;
            result.ServiceEnsured = install.Success;
        }
        else
        {
            var status = AegisServiceInstaller.GetStatus();
            result.ServiceEnsured = status.Installed;
            if (status.Installed && !status.Running)
            {
                try
                {
                    var start = RunSc($"start {AegisProduct.RecoveryServiceName}");
                    result.ServiceEnsured = start.ExitCode == 0 || start.ExitCode == 1056;
                }
                catch
                {
                    result.ServiceEnsured = false;
                }
            }
        }

        AegisAclHardening.ApplyMirrorRootAcls();
        result.LayoutReady = true;
        result.Message = "보호 스택 준비 완료";
        return result;
    }

    private static (int ExitCode, string Output) RunSc(string arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Trim());
    }
}

public sealed class AegisProvisionResult
{
    public bool LayoutReady { get; set; }
    public bool ServiceEnsured { get; set; }
    public bool ServiceInstallAttempted { get; set; }
    public string ServiceInstallMessage { get; set; } = "";
    public bool OfflineEnsured { get; set; }
    public bool BackupEnsured { get; set; }
    public string Message { get; set; } = "";
}