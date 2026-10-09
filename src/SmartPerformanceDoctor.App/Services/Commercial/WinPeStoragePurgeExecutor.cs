using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// WinPE-only device sanitizer. The desktop UI exposes it only after WinPE-specific approval.
/// It permits only the documented Windows storage reinitialize path for NVMe Crypto/Block Erase,
/// after the separate pre-execution gate has passed. It never runs in normal Windows.
/// </summary>
public sealed class WinPeStoragePurgeExecutor
{
    private readonly IWinPePurgeVolumeLockCoordinator _volumeLocks;
    private readonly IStorageReinitializeMediaApi _native;
    private readonly INvmeSanitizeStatusReadOnlyReader _statusReader;
    private readonly Func<bool> _isWinPe;
    private readonly IStoragePurgeExecutionEvidence _executionEvidence;

    public WinPeStoragePurgeExecutor(
        IWinPePurgeVolumeLockCoordinator? volumeLocks = null,
        IStorageReinitializeMediaApi? native = null,
        INvmeSanitizeStatusReadOnlyReader? statusReader = null,
        Func<bool>? isWinPe = null,
        IStoragePurgeExecutionEvidence? executionEvidence = null)
    {
        _volumeLocks = volumeLocks ?? new WindowsWinPePurgeVolumeLockCoordinator();
        _native = native ?? new WindowsStorageReinitializeMediaApi();
        _statusReader = statusReader ?? new WindowsNvmeSanitizeStatusReadOnlyReader();
        _isWinPe = isWinPe ?? IsCurrentWinPe;
        _executionEvidence = executionEvidence ?? new StoragePurgeExecutionEvidenceService();
    }

    public static string RequiredConfirmation(StorageSanitizeMethod method, int? diskNumber) =>
        $"EXECUTE {method} PURGE DISK {diskNumber}";

    public async Task<WinPePurgeExecutionResult> ExecuteAsync(
        WinPePurgeExecutionRequest request,
        IProgress<WinPePurgeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateBeforeDispatch(request);
        cancellationToken.ThrowIfCancellationRequested();
        var evidence = _executionEvidence.Begin(request);
        var dispatchStarted = false;
        try
        {
            progress?.Report(new WinPePurgeProgress("volume-lock", 5, "대상 디스크의 마운트된 볼륨을 잠그고 캐시를 비웁니다."));
            using var lease = _volumeLocks.Acquire(request.Plan.Device.DiskNumber!.Value);
            if (!lease.IsLocked) throw new InvalidOperationException("대상 볼륨 잠금을 확인하지 못했습니다.");

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new WinPePurgeProgress("dispatch", 15, "저장장치 재초기화 명령을 전송합니다. 이 시점부터 취소할 수 없습니다."));
            // If the native call throws or times out after this point, it is unsafe to
            // infer that the controller received no command. Preserve the uncertainty.
            dispatchStarted = true;
            _native.ReinitializeMedia(request.Plan.Device.DiskNumber!.Value, request.Method, request.Timeout);
            var dispatched = DateTimeOffset.UtcNow;

            // Cancellation is deliberately not observed after dispatch: releasing a lock while a
            // controller sanitization is still active would be less safe than continuing monitoring.
            for (var attempt = 0; attempt < request.MaximumStatusPolls; attempt++)
            {
                var status = _statusReader.Read(request.Plan);
                if (!status.IsCurrentFor(request.Plan, DateTimeOffset.UtcNow) || status.CollectedAtUtc < dispatched)
                    return CompleteEvidence(evidence, new WinPePurgeExecutionResult(true, false, "전송 뒤의 동일 장치·최신 상태 증적을 확인하지 못했습니다. 완료로 판정하지 않습니다.", status, dispatched));
                if (!status.ReadSucceeded)
                    return CompleteEvidence(evidence, new WinPePurgeExecutionResult(true, false, "상태 로그를 읽지 못했습니다. 명령 제출 여부는 감사 증적으로 검토해야 합니다.", status, dispatched));
                if (status.State == NvmeSanitizeOperationState.Failed)
                    return CompleteEvidence(evidence, new WinPePurgeExecutionResult(true, false, "NVMe가 Sanitize 실패 상태를 보고했습니다. 장치를 재사용하지 말고 제조사 절차를 따르세요.", status, dispatched));
                if (status.State is NvmeSanitizeOperationState.Succeeded or NvmeSanitizeOperationState.SucceededWithForcedDeallocation)
                    return CompleteEvidence(evidence, new WinPePurgeExecutionResult(true, true, "NVMe 상태 로그가 Sanitize 완료를 보고했습니다. 이는 컨트롤러 보고이며 독립 복구 불가 보증은 아닙니다.", status, dispatched));

                var percent = status.ProgressBasisPoints is int points ? Math.Clamp(points / 100, 15, 99) : 15;
                progress?.Report(new WinPePurgeProgress("monitor", percent, status.State == NvmeSanitizeOperationState.InProgress ? "장치 Sanitize 진행 상태를 확인 중입니다." : "장치 상태 전환을 확인 중입니다."));
                await Task.Delay(request.StatusPollInterval).ConfigureAwait(false);
            }

            var finalStatus = _statusReader.Read(request.Plan);
            return CompleteEvidence(evidence, new WinPePurgeExecutionResult(true, false, "상태 확인 시간 제한에 도달했습니다. 장치 상태를 WinPE에서 다시 확인하세요.", finalStatus, dispatched));
        }
        catch (Exception exception)
        {
            var message = dispatchStarted
                ? "장치 명령 전송 API가 오류를 반환했습니다. 컨트롤러가 명령을 받았는지 알 수 없으므로 장치를 재사용하지 말고 WinPE에서 상태를 확인하세요: " + exception.Message
                : "장치 명령 전송 전 오류가 발생했습니다. 명령은 전송하지 않았습니다: " + exception.Message;
            return CompleteEvidence(evidence, new WinPePurgeExecutionResult(false, false, message, null, DateTimeOffset.UtcNow)
            {
                DispatchState = dispatchStarted ? PurgeDispatchState.SubmissionUncertain : PurgeDispatchState.NotAttempted
            });
        }
    }

    private WinPePurgeExecutionResult CompleteEvidence(PurgeExecutionEvidenceSession evidence, WinPePurgeExecutionResult result)
    {
        try
        {
            var receipt = _executionEvidence.Complete(evidence, result);
            return result with { EvidencePath = receipt.Path, EvidenceValid = receipt.IsValid };
        }
        catch (Exception exception)
        {
            return result with { Message = result.Message + " 실행 증적 기록 실패: " + exception.Message, EvidenceValid = false };
        }
    }

    private void ValidateBeforeDispatch(WinPePurgeExecutionRequest request)
    {
        if (!OperatingSystem.IsWindows() || !_isWinPe() || !request.Plan.IsWinPe)
            throw new InvalidOperationException("장치 PURGE 실행은 WinPE에서만 허용됩니다.");
        if (request.Plan.Device.IsBootDisk || request.Plan.Device.IsSystemDisk)
            throw new InvalidOperationException("현재 WinPE 부팅/시스템 디스크는 Purge 대상으로 실행할 수 없습니다.");
        if (!request.Assessment.IsEligibleForFutureExecutor || request.Assessment.ExecutionEnabled)
            throw new InvalidOperationException("비파괴 사전 실행 게이트를 통과하지 못했습니다.");
        if (!request.Authorization.AuthorizedForFutureExecutor || !DeviceFingerprint.FixedTimeEquals(DeviceFingerprint.Create(request.Plan.Device), request.Authorization.DeviceFingerprint))
            throw new InvalidOperationException("현재 디스크에 결속된 일회용 승인이 없습니다.");
        if (!string.Equals(request.ConfirmationPhrase?.Trim(), RequiredConfirmation(request.Method, request.Plan.Device.DiskNumber), StringComparison.Ordinal))
            throw new InvalidOperationException("최종 실행 확인 문구가 일치하지 않습니다.");
        if (request.Method is not (StorageSanitizeMethod.CryptoErase or StorageSanitizeMethod.BlockErase))
            throw new InvalidOperationException("이 실행기는 NVMe Crypto Erase 또는 Block Erase만 지원합니다.");
        if (!string.Equals(request.Plan.Device.BusType, "NVMe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("이 실행기는 NVMe 장치만 지원합니다.");
        if (request.Method == StorageSanitizeMethod.CryptoErase && !request.Assessment.CrossCheck.CryptoEraseCrossValidated)
            throw new InvalidOperationException("Crypto Erase 지원이 두 개의 읽기 전용 장치 증적으로 교차 확인되지 않았습니다.");
        if (request.Method == StorageSanitizeMethod.BlockErase && !request.Assessment.CrossCheck.BlockEraseCrossValidated)
            throw new InvalidOperationException("Block Erase 지원이 두 개의 읽기 전용 장치 증적으로 교차 확인되지 않았습니다.");
        if (request.Timeout < TimeSpan.FromSeconds(60) || request.Timeout > TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(request.Timeout), "허용된 장치 작업 시간은 60초~24시간입니다.");
        if (request.MaximumStatusPolls is < 1 or > 17_280)
            throw new ArgumentOutOfRangeException(nameof(request.MaximumStatusPolls));
    }

    private static bool IsCurrentWinPe()
    {
        var root = Environment.GetEnvironmentVariable("SystemRoot");
        return !string.IsNullOrWhiteSpace(root) && File.Exists(Path.Combine(root, "System32", "wpeutil.exe"));
    }
}

public sealed class WinPePurgeExecutionRequest
{
    public StorageSanitizeDryRunResult Plan { get; init; } = new();
    public PurgeAuthorizationDecision Authorization { get; init; } = new();
    public PurgePreExecutionAssessment Assessment { get; init; } = new();
    public StorageSanitizeMethod Method { get; init; }
    public string ConfirmationPhrase { get; init; } = "";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(2);
    public TimeSpan StatusPollInterval { get; init; } = TimeSpan.FromSeconds(5);
    public int MaximumStatusPolls { get; init; } = 1_440;
}

public sealed record WinPePurgeProgress(string Stage, int Percent, string Detail);

public enum PurgeDispatchState
{
    NotAttempted,
    NativeCallReturned,
    SubmissionUncertain
}

public sealed record WinPePurgeExecutionResult(bool CommandIssued, bool Completed, string Message, NvmeSanitizeStatusReadOnlyEvidence? FinalStatus, DateTimeOffset DispatchedAtUtc)
{
    public bool ExecutionEnabled => CommandIssued;
    public string? EvidencePath { get; init; }
    public bool EvidenceValid { get; init; }
    public PurgeDispatchState DispatchState { get; init; } = CommandIssued ? PurgeDispatchState.NativeCallReturned : PurgeDispatchState.NotAttempted;
}

public interface IStorageReinitializeMediaApi
{
    void ReinitializeMedia(int diskNumber, StorageSanitizeMethod method, TimeSpan timeout);
}

public interface IWinPePurgeVolumeLockCoordinator
{
    IWinPePurgeVolumeLockLease Acquire(int diskNumber);
}

public interface IWinPePurgeVolumeLockLease : IDisposable
{
    bool IsLocked { get; }
}

/// <summary>Uses the documented IOCTL_STORAGE_REINITIALIZE_MEDIA path; reachable only through WinPeStoragePurgeExecutor.</summary>
public sealed class WindowsStorageReinitializeMediaApi : IStorageReinitializeMediaApi
{
    private const uint IoctlStorageReinitializeMedia = 0x002D9640;
    public void ReinitializeMedia(int diskNumber, StorageSanitizeMethod method, TimeSpan timeout)
    {
        var nativeMethod = method switch { StorageSanitizeMethod.BlockErase => 1u, StorageSanitizeMethod.CryptoErase => 2u, _ => throw new ArgumentOutOfRangeException(nameof(method)) };
        var input = new byte[16];
        BitConverter.GetBytes(16u).CopyTo(input, 0); BitConverter.GetBytes(16u).CopyTo(input, 4);
        BitConverter.GetBytes((uint)Math.Ceiling(timeout.TotalSeconds)).CopyTo(input, 8);
        BitConverter.GetBytes(nativeMethod | (1u << 4)).CopyTo(input, 12); // prohibit unrestricted sanitize exit after dispatch
        using SafeFileHandle handle = File.OpenHandle("\\\\.\\PhysicalDrive" + diskNumber, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        if (!DeviceIoControl(handle, IoctlStorageReinitializeMedia, input, input.Length, IntPtr.Zero, 0, out _, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint code, byte[] input, int inputLength, IntPtr output, int outputLength, out int bytesReturned, IntPtr overlapped);
}

/// <summary>Locks every mounted drive letter returned for the target disk; any lock failure aborts before dispatch.</summary>
public sealed class WindowsWinPePurgeVolumeLockCoordinator : IWinPePurgeVolumeLockCoordinator
{
    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlUnlockVolume = 0x0009001C;
    public IWinPePurgeVolumeLockLease Acquire(int diskNumber)
    {
        var letters = DiscoverMountedLetters(diskNumber);
        var handles = new List<SafeFileHandle>();
        try
        {
            foreach (var letter in letters)
            {
                var handle = File.OpenHandle("\\\\.\\" + letter + ":", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                if (!DeviceIoControl(handle, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero)) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error(), $"{letter}: 볼륨을 잠글 수 없습니다."); }
                handles.Add(handle);
            }
            return new Lease(handles);
        }
        catch { foreach (var handle in handles) handle.Dispose(); throw; }
    }
    private static string[] DiscoverMountedLetters(int diskNumber)
    {
        var script = "$ErrorActionPreference='Stop'; Get-Partition -DiskNumber " + diskNumber + " | Where-Object { $_.DriveLetter } | ForEach-Object { $_.DriveLetter }";
        using var process = Process.Start(new ProcessStartInfo { FileName = "powershell.exe", Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
        if (process is null) throw new IOException("대상 디스크의 볼륨 목록을 읽지 못했습니다.");
        var output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(6000)) { try { process.Kill(true); } catch { } throw new TimeoutException("대상 디스크의 볼륨 조회 시간이 초과되었습니다."); }
        if (process.ExitCode != 0) throw new IOException("대상 디스크의 볼륨 목록을 읽지 못했습니다.");
        return output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(letter => letter.Length == 1 && char.IsLetter(letter[0])).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private sealed class Lease(List<SafeFileHandle> handles) : IWinPePurgeVolumeLockLease
    {
        public bool IsLocked => true;
        public void Dispose() { foreach (var handle in handles) { try { DeviceIoControl(handle, FsctlUnlockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero); } catch { } handle.Dispose(); } }
    }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint code, IntPtr input, int inputLength, IntPtr output, int outputLength, out int bytesReturned, IntPtr overlapped);
}
