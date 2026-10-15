using System.ComponentModel;
using System.Diagnostics;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App.Services.Update;

public enum RecoveryLaunchResult { Launched, Cancelled, HelperMissing, Failed }

public static class UpdateRecoveryCoordinator
{
    public static string WorkRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PC-Care", "UpdateTransactions");

    public static UpdateRecoveryStatus Inspect()
    {
        var transaction=new UpdateTransaction(SmartPerformanceDoctor.UpdateEngine.BuildPublisherTrust.Current,WorkRoot, strictAcl: true);
        var status=transaction.InspectRecovery(UpdatePaths.AppInstallDirectory);
        if(status.State is UpdateRecoveryState.NoPendingTransaction or UpdateRecoveryState.RecoveryCompleted)
        {
            var attestation=transaction.InspectInstallationTrust(UpdatePaths.AppInstallDirectory);
            if(attestation.RequiresReview)return new(UpdateRecoveryState.RecoveryRequired,attestation.Code,"");
            if(attestation.Verified)return new(UpdateRecoveryState.NoPendingTransaction,attestation.Code,"");
        }
        return status;
    }

    public static RecoveryLaunchResult LaunchElevatedRecovery()
    {
        var helper = Path.Combine(UpdatePaths.AppInstallDirectory, "engine",
            "SmartPerformanceDoctor.UpdateHelper.exe");
        try
        {
            try
            {
                if (File.Exists(helper) && UpdateBootstrap.LaunchInstalled(UpdatePaths.AppInstallDirectory, "--recover", "", Environment.ProcessId) is not null)
                    return RecoveryLaunchResult.Launched;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
            return UpdateBootstrap.LaunchSnapshotRecovery(UpdatePaths.AppInstallDirectory, Environment.ProcessId) is null
                ? RecoveryLaunchResult.HelperMissing : RecoveryLaunchResult.Launched;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { return RecoveryLaunchResult.Cancelled; }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or InvalidDataException or UnauthorizedAccessException)
        { return RecoveryLaunchResult.Failed; }
    }
    public static bool CanRequestLegacyRecovery()
    {
        try { var files=BootstrapDispatch.PinBridge();foreach(var file in files)file.Dispose();return true; }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){return false;}
    }
    public static LegacyRecoveryPlan ReviewLegacy(string package,string previousVersion)
    {
        var transaction=new UpdateTransaction(BuildPublisherTrust.Current,WorkRoot, strictAcl: true);
        var records=transaction.DiagnoseLegacy(UpdatePaths.AppInstallDirectory);
        if(records.Count!=1)throw new InvalidDataException("단일 구형 거래를 확인하지 못했습니다.");
        return transaction.PlanLegacyRecovery(UpdatePaths.AppInstallDirectory,records[0].TransactionId,package,previousVersion);
    }
    public static BootstrapDispatchResult LaunchLegacy(LegacyRecoveryPlan plan,string package,string previousVersion,bool consent)
    {
        if(!consent||plan.Targets.Count==0||plan.Targets.Any(t=>t.Status!="BackupVerified"))
            return BootstrapDispatch.Result(BootstrapTransitionState.RecoveryRequired);
        if(!File.Exists(Path.Combine(BootstrapDispatch.BridgeRoot,"SmartPerformanceDoctor.UpdateBridge.exe")))
            return BootstrapDispatch.Result(BootstrapTransitionState.BridgeUnavailable);
        try
        {
            var leases=BootstrapDispatch.PinBridge();
            try
            {
                UpdatePaths.EnsureLayout();
                var path=Path.Combine(UpdatePaths.Inbox,"legacy-request-"+Guid.NewGuid().ToString("N")+".json");
                using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {System.Text.Json.JsonSerializer.Serialize(file,new LegacyRestoreRequest(plan,Path.GetFullPath(package),previousVersion) { Approval = new(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,true) });file.Flush(true);}
                using var requestPin=WindowsFileBoundary.OpenRead(Path.GetDirectoryName(path)!,Path.GetFileName(path));
                var requestHash=requestPin.Hash();
                var start=new ProcessStartInfo(Path.Combine(BootstrapDispatch.BridgeRoot,"SmartPerformanceDoctor.UpdateBridge.exe"))
                    {UseShellExecute=true,Verb="runas",WorkingDirectory=BootstrapDispatch.BridgeRoot};
                foreach(var arg in new[]{"--legacy-restore",UpdatePaths.AppInstallDirectory,path,requestHash,Environment.ProcessId.ToString()})start.ArgumentList.Add(arg);
                using var child=Process.Start(start);
                return BootstrapDispatch.Result(child is null?BootstrapTransitionState.RecoveryRequired:BootstrapTransitionState.TransitionLaunched);
            }
            finally{foreach(var lease in leases)lease.Dispose();}
        }
        catch(Win32Exception ex) when(ex.NativeErrorCode==1223){return BootstrapDispatch.Result(BootstrapTransitionState.ElevationCancelled);}
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {return BootstrapDispatch.Result(BootstrapTransitionState.BridgeUntrusted);}
    }
}
