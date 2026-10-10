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
        new UpdateTransaction(SmartPerformanceDoctor.UpdateEngine.BuildPublisherTrust.Current, WorkRoot, strictAcl: false)
            .InspectRecovery(UpdatePaths.AppInstallDirectory);

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
}
