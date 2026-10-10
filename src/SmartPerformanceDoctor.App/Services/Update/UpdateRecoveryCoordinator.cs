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

    public static UpdateRecoveryStatus Inspect() =>
        new UpdateTransaction(PublisherTrustPolicy.Production, WorkRoot, strictAcl: false)
            .InspectRecovery(UpdatePaths.AppInstallDirectory);

    public static RecoveryLaunchResult LaunchElevatedRecovery()
    {
        var helper = Path.Combine(UpdatePaths.AppInstallDirectory, "engine",
            "SmartPerformanceDoctor.UpdateHelper.exe");
        if (!File.Exists(helper) || (File.GetAttributes(helper) & FileAttributes.ReparsePoint) != 0)
            return RecoveryLaunchResult.HelperMissing;
        var info = new ProcessStartInfo(helper) { UseShellExecute = true, Verb = "runas" };
        info.ArgumentList.Add("--recover");
        info.ArgumentList.Add("--wait-pid");
        info.ArgumentList.Add(Environment.ProcessId.ToString());
        try { return Process.Start(info) is null ? RecoveryLaunchResult.Failed : RecoveryLaunchResult.Launched; }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { return RecoveryLaunchResult.Cancelled; }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { return RecoveryLaunchResult.Failed; }
    }
}
