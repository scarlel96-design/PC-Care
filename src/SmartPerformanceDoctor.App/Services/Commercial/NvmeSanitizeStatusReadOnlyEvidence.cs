using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public enum NvmeSanitizeOperationState
{
    Unknown,
    NeverSanitized,
    Succeeded,
    InProgress,
    Failed,
    SucceededWithForcedDeallocation
}

/// <summary>Read-only snapshot of NVMe Sanitize Status log page 81h. It can detect an existing operation, never start one.</summary>
public sealed class NvmeSanitizeStatusReadOnlyEvidence
{
    public string DeviceFingerprint { get; init; } = "";
    public DateTimeOffset CollectedAtUtc { get; init; }
    public bool ReadSucceeded { get; init; }
    public NvmeSanitizeOperationState State { get; init; }
    public int? ProgressBasisPoints { get; init; }
    public int CompletedOverwritePasses { get; init; }
    public bool GlobalDataErasedSinceLastSuccess { get; init; }
    public uint EstimatedCryptoEraseSeconds { get; init; }
    public string StatusLogSha256 { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool CanExecute => false;

    public bool IsCurrentFor(StorageSanitizeDryRunResult plan, DateTimeOffset now) =>
        ReadSucceeded && CollectedAtUtc <= now && now - CollectedAtUtc <= TimeSpan.FromMinutes(5) &&
        global::SmartPerformanceDoctor.App.Services.Commercial.DeviceFingerprint.FixedTimeEquals(DeviceFingerprint, global::SmartPerformanceDoctor.App.Services.Commercial.DeviceFingerprint.Create(plan.Device));
}

public interface INvmeSanitizeStatusReadOnlyReader
{
    NvmeSanitizeStatusReadOnlyEvidence Read(StorageSanitizeDryRunResult plan);
}

/// <summary>Reads NVMe log page 81h using IOCTL_STORAGE_QUERY_PROPERTY only.</summary>
public sealed class WindowsNvmeSanitizeStatusReadOnlyReader : INvmeSanitizeStatusReadOnlyReader
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageAdapterProtocolSpecificProperty = 49;
    private const int ProtocolTypeNvme = 3;
    private const int NvmeDataTypeLogPage = 2;
    private const int SanitizeStatusLog = 0x81;
    private const int PropertyQueryBytes = 8;
    private const int ProtocolSpecificBytes = 40;
    private const int LogBytes = 512;

    public NvmeSanitizeStatusReadOnlyEvidence Read(StorageSanitizeDryRunResult plan)
    {
        var fingerprint = DeviceFingerprint.Create(plan.Device);
        if (!plan.Device.IsResolved || !string.Equals(plan.Device.BusType, "NVMe", StringComparison.OrdinalIgnoreCase)) return Failed(fingerprint, "NVMe 물리 디스크가 아니거나 장치 식별이 완료되지 않았습니다.");
        if (!OperatingSystem.IsWindows()) return Failed(fingerprint, "NVMe Sanitize Status 읽기 전용 수집은 Windows에서만 지원합니다.");
        try { return NvmeSanitizeStatusEvidenceParser.Parse(QueryStatusLog(plan.Device.DiskNumber!.Value), fingerprint, DateTimeOffset.UtcNow); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception) { return Failed(fingerprint, "NVMe Sanitize Status 읽기 전용 수집 실패: " + exception.Message); }
    }

    private static NvmeSanitizeStatusReadOnlyEvidence Failed(string fingerprint, string detail) => new() { DeviceFingerprint = fingerprint, CollectedAtUtc = DateTimeOffset.UtcNow, Detail = detail };

    private static byte[] QueryStatusLog(int diskNumber)
    {
        var input = new byte[PropertyQueryBytes + ProtocolSpecificBytes];
        WriteInt(input, 0, StorageAdapterProtocolSpecificProperty); WriteInt(input, 4, 0); WriteInt(input, 8, ProtocolTypeNvme);
        WriteInt(input, 12, NvmeDataTypeLogPage); WriteInt(input, 16, SanitizeStatusLog); WriteInt(input, 24, ProtocolSpecificBytes); WriteInt(input, 28, LogBytes);
        var output = new byte[PropertyQueryBytes + ProtocolSpecificBytes + LogBytes];
        using SafeFileHandle handle = File.OpenHandle("\\\\.\\PhysicalDrive" + diskNumber, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (!DeviceIoControl(handle, IoctlStorageQueryProperty, input, input.Length, output, output.Length, out var returned, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var dataStart = PropertyQueryBytes + ProtocolSpecificBytes;
        if (returned < dataStart + LogBytes) throw new IOException("NVMe Sanitize Status 응답 길이가 부족합니다.");
        return output.AsSpan(dataStart, LogBytes).ToArray();
    }

    private static void WriteInt(byte[] buffer, int offset, int value) => BitConverter.GetBytes(value).CopyTo(buffer, offset);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, byte[] input, int inputLength, byte[] output, int outputLength, out int bytesReturned, IntPtr overlapped);
}

public static class NvmeSanitizeStatusEvidenceParser
{
    private const int LogBytes = 512;
    public static NvmeSanitizeStatusReadOnlyEvidence Parse(ReadOnlySpan<byte> statusLog, string deviceFingerprint, DateTimeOffset collectedAtUtc)
    {
        if (statusLog.Length != LogBytes) return new NvmeSanitizeStatusReadOnlyEvidence { DeviceFingerprint = deviceFingerprint, CollectedAtUtc = collectedAtUtc, Detail = "NVMe Sanitize Status 응답 길이가 512바이트가 아닙니다. 증거로 사용하지 않습니다." };
        var progress = BitConverter.ToUInt16(statusLog.Slice(0, 2));
        var stateBits = (byte)(BitConverter.ToUInt16(statusLog.Slice(2, 2)) & 0b111);
        var passes = (BitConverter.ToUInt16(statusLog.Slice(2, 2)) >> 3) & 0b1111;
        var erased = (BitConverter.ToUInt16(statusLog.Slice(2, 2)) & (1 << 7)) != 0;
        var state = stateBits switch { 0 => NvmeSanitizeOperationState.NeverSanitized, 1 => NvmeSanitizeOperationState.Succeeded, 2 => NvmeSanitizeOperationState.InProgress, 3 => NvmeSanitizeOperationState.Failed, 4 => NvmeSanitizeOperationState.SucceededWithForcedDeallocation, _ => NvmeSanitizeOperationState.Unknown };
        return new NvmeSanitizeStatusReadOnlyEvidence
        {
            DeviceFingerprint = deviceFingerprint, CollectedAtUtc = collectedAtUtc, ReadSucceeded = true, State = state,
            ProgressBasisPoints = state == NvmeSanitizeOperationState.InProgress ? (int)Math.Round(progress * 10000d / 65535d) : null,
            CompletedOverwritePasses = passes, GlobalDataErasedSinceLastSuccess = erased,
            EstimatedCryptoEraseSeconds = BitConverter.ToUInt32(statusLog.Slice(16, 4)),
            StatusLogSha256 = Convert.ToHexString(SHA256.HashData(statusLog)).ToLowerInvariant(),
            Detail = "NVMe Sanitize Status(81h)를 읽기 전용으로 수집했습니다. 기존 작업 상태 감지용이며 실행·완료 증명은 아닙니다."
        };
    }
}