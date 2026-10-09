using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>Read-only evidence from NVMe Commands Supported and Effects Log (log page 05h).</summary>
public sealed class NvmeCommandEffectsReadOnlyEvidence
{
    public string DeviceFingerprint { get; init; } = "";
    public DateTimeOffset CollectedAtUtc { get; init; }
    public bool ReadSucceeded { get; init; }
    public bool SanitizeCommandSupported { get; init; }
    public bool SanitizeMayChangeControllerCapabilities { get; init; }
    public string EffectsLogSha256 { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool CanExecute => false;

    public bool IsCurrentFor(StorageSanitizeDryRunResult plan, DateTimeOffset now) =>
        ReadSucceeded && CollectedAtUtc <= now && now - CollectedAtUtc <= TimeSpan.FromMinutes(5) &&
        global::SmartPerformanceDoctor.App.Services.Commercial.DeviceFingerprint.FixedTimeEquals(DeviceFingerprint, global::SmartPerformanceDoctor.App.Services.Commercial.DeviceFingerprint.Create(plan.Device));
}

public interface INvmeCommandEffectsReadOnlyReader
{
    NvmeCommandEffectsReadOnlyEvidence Read(StorageSanitizeDryRunResult plan);
}

/// <summary>
/// Reads only log page 05h through IOCTL_STORAGE_QUERY_PROPERTY. It does not use IOCTL_STORAGE_PROTOCOL_COMMAND
/// and cannot send an NVMe Sanitize, Format, Set Feature, or vendor-specific command.
/// </summary>
public sealed class WindowsNvmeCommandEffectsReadOnlyReader : INvmeCommandEffectsReadOnlyReader
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageAdapterProtocolSpecificProperty = 49;
    private const int ProtocolTypeNvme = 3;
    private const int NvmeDataTypeLogPage = 2;
    private const int CommandsSupportedAndEffectsLog = 0x05;
    private const int PropertyQueryBytes = 8;
    private const int ProtocolSpecificBytes = 40;
    private const int LogBytes = 4096;

    public NvmeCommandEffectsReadOnlyEvidence Read(StorageSanitizeDryRunResult plan)
    {
        var fingerprint = DeviceFingerprint.Create(plan.Device);
        if (!plan.Device.IsResolved || !string.Equals(plan.Device.BusType, "NVMe", StringComparison.OrdinalIgnoreCase))
            return Failed(fingerprint, "NVMe 물리 디스크가 아니거나 장치 식별이 완료되지 않았습니다.");
        if (!OperatingSystem.IsWindows()) return Failed(fingerprint, "NVMe 효과 로그 읽기 전용 수집은 Windows에서만 지원합니다.");
        try
        {
            var data = QueryEffectsLog(plan.Device.DiskNumber!.Value);
            return NvmeCommandEffectsEvidenceParser.Parse(data, fingerprint, DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return Failed(fingerprint, "NVMe Command Effects Log 읽기 전용 수집 실패: " + exception.Message);
        }
    }

    private static NvmeCommandEffectsReadOnlyEvidence Failed(string fingerprint, string detail) => new()
    {
        DeviceFingerprint = fingerprint, CollectedAtUtc = DateTimeOffset.UtcNow, Detail = detail
    };

    private static byte[] QueryEffectsLog(int diskNumber)
    {
        var input = new byte[PropertyQueryBytes + ProtocolSpecificBytes];
        WriteInt(input, 0, StorageAdapterProtocolSpecificProperty);
        WriteInt(input, 4, 0); // PropertyStandardQuery
        WriteInt(input, 8, ProtocolTypeNvme);
        WriteInt(input, 12, NvmeDataTypeLogPage);
        WriteInt(input, 16, CommandsSupportedAndEffectsLog);
        WriteInt(input, 24, ProtocolSpecificBytes);
        WriteInt(input, 28, LogBytes);
        var output = new byte[PropertyQueryBytes + ProtocolSpecificBytes + LogBytes];
        using SafeFileHandle handle = File.OpenHandle("\\\\.\\PhysicalDrive" + diskNumber, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (!DeviceIoControl(handle, IoctlStorageQueryProperty, input, input.Length, output, output.Length, out var returned, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var dataStart = PropertyQueryBytes + ProtocolSpecificBytes;
        if (returned < dataStart + LogBytes) throw new IOException("NVMe Command Effects Log 응답 길이가 부족합니다.");
        return output.AsSpan(dataStart, LogBytes).ToArray();
    }

    private static void WriteInt(byte[] buffer, int offset, int value) => BitConverter.GetBytes(value).CopyTo(buffer, offset);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, byte[] input, int inputLength, byte[] output, int outputLength, out int bytesReturned, IntPtr overlapped);
}

public static class NvmeCommandEffectsEvidenceParser
{
    private const int LogBytes = 4096;
    private const int SanitizeOpcode = 0x84;
    private const int CommandEffectBytes = 4;

    public static NvmeCommandEffectsReadOnlyEvidence Parse(ReadOnlySpan<byte> commandEffectsLog, string deviceFingerprint, DateTimeOffset collectedAtUtc)
    {
        if (commandEffectsLog.Length != LogBytes)
            return new NvmeCommandEffectsReadOnlyEvidence
            {
                DeviceFingerprint = deviceFingerprint, CollectedAtUtc = collectedAtUtc,
                Detail = "NVMe Command Effects Log 응답 길이가 4096바이트가 아닙니다. 증거로 사용하지 않습니다."
            };

        var sanitizeEffect = BitConverter.ToUInt32(commandEffectsLog.Slice(SanitizeOpcode * CommandEffectBytes, CommandEffectBytes));
        return new NvmeCommandEffectsReadOnlyEvidence
        {
            DeviceFingerprint = deviceFingerprint,
            CollectedAtUtc = collectedAtUtc,
            ReadSucceeded = true,
            SanitizeCommandSupported = (sanitizeEffect & 0b1) != 0,
            SanitizeMayChangeControllerCapabilities = (sanitizeEffect & (1u << 4)) != 0,
            EffectsLogSha256 = Convert.ToHexString(SHA256.HashData(commandEffectsLog)).ToLowerInvariant(),
            Detail = "NVMe Command Supported and Effects Log의 Sanitize(84h) 항목을 읽기 전용으로 수집했습니다. 실행 승인이나 완료 증명은 아닙니다."
        };
    }
}

/// <summary>Requires two independently read-only controller reports to agree before a method is called cross-validated.</summary>
public sealed class NvmePurgeCapabilityCrossCheck
{
    public bool IsCurrent { get; init; }
    public bool SanitizeCommandSupported { get; init; }
    public bool CryptoEraseCrossValidated { get; init; }
    public bool BlockEraseCrossValidated { get; init; }
    public bool OverwriteCrossValidated { get; init; }
    public string Detail { get; init; } = "";
    public bool CanExecute => false;

    public static NvmePurgeCapabilityCrossCheck Evaluate(StorageSanitizeDryRunResult plan, NvmeIdentifyReadOnlyEvidence? identify, NvmeCommandEffectsReadOnlyEvidence? effects, DateTimeOffset now)
    {
        var identifyCurrent = identify?.IsCurrentFor(plan, now) == true;
        var effectsCurrent = effects?.IsCurrentFor(plan, now) == true;
        var commandSupported = effectsCurrent && effects!.SanitizeCommandSupported;
        if (!identifyCurrent || !effectsCurrent)
            return new NvmePurgeCapabilityCrossCheck { Detail = "Identify 또는 Command Effects Log의 최신 장치 결속 증적이 부족합니다." };
        if (!commandSupported)
            return new NvmePurgeCapabilityCrossCheck { IsCurrent = true, Detail = "Command Effects Log에서 Sanitize(84h)를 지원하지 않는 것으로 보고했습니다." };
        return new NvmePurgeCapabilityCrossCheck
        {
            IsCurrent = true,
            SanitizeCommandSupported = true,
            CryptoEraseCrossValidated = identify!.SupportsCryptoErase,
            BlockEraseCrossValidated = identify.SupportsBlockErase,
            OverwriteCrossValidated = identify.SupportsOverwrite,
            Detail = "Identify SANICAP과 Command Effects Log가 동일 장치의 Sanitize 지원을 읽기 전용으로 교차 확인했습니다. 실행·완료 증명은 아닙니다."
        };
    }
}