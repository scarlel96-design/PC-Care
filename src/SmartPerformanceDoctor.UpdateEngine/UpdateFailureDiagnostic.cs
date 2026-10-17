using System.ComponentModel;
namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record UpdateFailureDiagnostic(string ExceptionType, string HResult, int? NativeErrorCode,
    string Phase, UpdateStage JournalStage, string TargetIdentity, string InstallVolume, string StagingVolume,
    bool StrictAclChecksRequired, bool AnyMutationStarted, string AccessContract, string ShareMode, string DeletePending)
{
    public static UpdateFailureDiagnostic Capture(Exception error, string phase, UpdateStage stage, string identity,
        string installVolume, string stagingVolume, bool strictAcl, bool mutationStarted)
    {
        int? native = null;
        for (Exception? cursor = error; cursor is not null; cursor = cursor.InnerException)
        {
            if (cursor is Win32Exception win32) { native = win32.NativeErrorCode; break; }
            if ((cursor.HResult & unchecked((int)0xFFFF0000)) == unchecked((int)0x80070000)) native ??= cursor.HResult & 0xFFFF;
        }
        return new(error.GetType().Name, $"0x{error.HResult:X8}", native, phase, stage, identity, installVolume, stagingVolume,
            strictAcl, mutationStarted, "Boundary: Read + ReadControl; Write for writable handles; Delete only for publication", "FILE_SHARE_READ", "NOT OBSERVED");
    }
}
