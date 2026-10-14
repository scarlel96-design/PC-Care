using System.ComponentModel;
using System.Diagnostics;
using System.Security.AccessControl;

namespace SmartPerformanceDoctor.UpdateEngine;

public enum BootstrapTransitionState { BootstrapNotRequired, BootstrapTransitionRequired, BridgeUnavailable, BridgeUntrusted, SignedPackageRejected, TransitionPrepared, AwaitingUserApproval, ElevationCancelled, TransitionLaunched, TransitionInterrupted, RecoveryRequired, TransitionVerified }
public sealed record BootstrapDispatchResult(BootstrapTransitionState State, string Code, string Message)
{ public bool ProcessLaunched => State==BootstrapTransitionState.TransitionLaunched; }

public static class BootstrapDispatch
{
    public static string BridgeRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PC-Care UpdateBridge");
    private static readonly string[] Files=["SmartPerformanceDoctor.UpdateBridge.exe","SmartPerformanceDoctor.UpdateBridge.dll","SmartPerformanceDoctor.UpdateEngine.dll"];
    public static BootstrapDispatchResult Describe(bool protocol,bool bridgePresent,bool bridgeTrusted,bool packageTrusted) =>
        !packageTrusted ? Result(BootstrapTransitionState.SignedPackageRejected) : protocol ? Result(BootstrapTransitionState.BootstrapNotRequired) :
        !bridgePresent ? Result(BootstrapTransitionState.BridgeUnavailable) : !bridgeTrusted ? Result(BootstrapTransitionState.BridgeUntrusted) : Result(BootstrapTransitionState.BootstrapTransitionRequired);
    public static BootstrapDispatchResult Result(BootstrapTransitionState state) => new(state,state.ToString(),state switch
    {
        BootstrapTransitionState.BootstrapNotRequired=>"기존의 검증된 업데이트 도우미를 사용합니다.",
        BootstrapTransitionState.BootstrapTransitionRequired=>"최초 업데이트 전환이 필요합니다. 관리자 승인 후 보호된 도우미를 준비합니다.",
        BootstrapTransitionState.BridgeUnavailable=>"검증된 전환 도구가 설치되지 않았습니다. 기존 설치와 기록을 보존했습니다.",
        BootstrapTransitionState.BridgeUntrusted=>"전환 도구의 발행자와 파일 신원을 확인하지 못했습니다.",
        BootstrapTransitionState.SignedPackageRejected=>"서명된 업데이트 패키지 검증에 실패했습니다.",
        BootstrapTransitionState.ElevationCancelled=>"관리자 승인이 취소됐습니다. 파일을 변경하지 않았습니다.",
        BootstrapTransitionState.TransitionLaunched=>"업데이트 도우미를 시작했습니다. 설치 완료 검증은 아직 대기 중입니다.",
        BootstrapTransitionState.TransitionVerified=>"설치 파일의 서명된 목록과 실제 해시를 확인했습니다.",
        BootstrapTransitionState.AwaitingUserApproval=>"Windows 관리자 승인을 기다립니다.",
        _=>"업데이트 상태의 추가 확인이 필요합니다. 원본 기록을 보존합니다."
    });
    public static List<WindowsFileBoundary.FileLease> PinBridge()
    {
        using var root=WindowsFileBoundary.PinDirectory(BridgeRoot);
        UpdateTransaction.ValidateProtectedSecurity(new DirectoryInfo(BridgeRoot).GetAccessControl(),false);
        var leases=new List<WindowsFileBoundary.FileLease>();
        try
        {
            foreach(var name in Files)
            {
                var file=WindowsFileBoundary.OpenRead(BridgeRoot,name); leases.Add(file);
                UpdateTransaction.ValidateProtectedSecurity(file.ReadSecurity(),false);
                var signature=AuthenticodeBoundary.Verify(Path.Combine(BridgeRoot,name));
                if(!AuthenticodeBoundary.IsBuildPublisher(signature)||signature.Identity!=file.Identity||signature.Hash!=file.Hash())
                    throw new InvalidDataException("BRIDGE-PUBLISHER-UNVERIFIED");
            }
            root.Validate();return leases;
        }
        catch {foreach(var lease in leases)lease.Dispose();throw;}
    }
    public static BootstrapDispatchResult InspectRoute(string installRoot)
    {
        try
        {
            UpdateBootstrap.ValidateInstallRoot(installRoot);
            if(UpdateBootstrap.SupportsSnapshotProtocol(Path.Combine(installRoot,"engine","SmartPerformanceDoctor.UpdateHelper.dll")))
            {UpdateBootstrap.ValidateInstalledRuntime(installRoot);return Result(BootstrapTransitionState.BootstrapNotRequired);}
            if(!File.Exists(Path.Combine(BridgeRoot,Files[0])))return Result(BootstrapTransitionState.BridgeUnavailable);
            var leases=PinBridge();foreach(var file in leases)file.Dispose();
            return Result(BootstrapTransitionState.BootstrapTransitionRequired);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {return Result(BootstrapTransitionState.BridgeUntrusted);}
    }
    public static BootstrapDispatchResult Launch(string installRoot,string packagePath,string currentVersion,int callerPid)
    {
        try
        {
            UpdateBootstrap.ValidateInstallRoot(installRoot);
            using var package=WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(packagePath))!,Path.GetFileName(packagePath));
            if(!SignedPackage.Verify(package.Stream,currentVersion,BuildPublisherTrust.Current).CanApply) return Result(BootstrapTransitionState.SignedPackageRejected);
            if(UpdateBootstrap.SupportsSnapshotProtocol(Path.Combine(installRoot,"engine","SmartPerformanceDoctor.UpdateHelper.dll")))
                return UpdateBootstrap.LaunchInstalled(installRoot,"--apply",packagePath,callerPid) is null ? Result(BootstrapTransitionState.RecoveryRequired) : Result(BootstrapTransitionState.TransitionLaunched);
            if(!File.Exists(Path.Combine(BridgeRoot,Files[0])))return Result(BootstrapTransitionState.BridgeUnavailable);
            var leases=PinBridge();
            try
            {
                var start=new ProcessStartInfo(Path.Combine(BridgeRoot,Files[0])){UseShellExecute=true,Verb="runas",WorkingDirectory=BridgeRoot};
                foreach(var arg in new[]{"--transition",Path.GetFullPath(installRoot),Path.GetFullPath(packagePath),callerPid.ToString()})start.ArgumentList.Add(arg);
                package.Validate();foreach(var lease in leases)lease.Validate();
                using var child=Process.Start(start);return Result(child is null?BootstrapTransitionState.RecoveryRequired:BootstrapTransitionState.TransitionLaunched);
            }
            finally {foreach(var lease in leases)lease.Dispose();}
        }
        catch(Win32Exception ex) when(ex.NativeErrorCode==1223){return Result(BootstrapTransitionState.ElevationCancelled);}
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {return Result(BootstrapTransitionState.BridgeUntrusted);}
    }
}
