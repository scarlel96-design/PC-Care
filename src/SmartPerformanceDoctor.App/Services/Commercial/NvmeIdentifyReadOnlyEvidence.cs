using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// Evidence parsed from the NVMe Identify Controller data structure. This object is evidence
/// of a controller-advertised capability only; it can never enable a Purge command.
/// </summary>
public sealed class NvmeIdentifyReadOnlyEvidence
{
    public string DeviceFingerprint { get; init; } = "";
    public DateTimeOffset CollectedAtUtc { get; init; }
    public bool ReadSucceeded { get; init; }
    public bool SupportsCryptoErase { get; init; }
    public bool SupportsBlockErase { get; init; }
    public bool SupportsOverwrite { get; init; }
    public bool NoDeallocateInhibited { get; init; }
    public string IdentifySha256 { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool CanExecute => false;

    public bool IsCurrentFor(StorageSanitizeDryRunResult plan, DateTimeOffset now) =>
        ReadSucceeded && CollectedAtUtc <= now && now - CollectedAtUtc <= TimeSpan.FromMinutes(5) &&
        global::SmartPerformanceDoctor.App.Services.Commercial.DeviceFingerprint.FixedTimeEquals(DeviceFingerprint, global::SmartPerformanceDoctor.App.Services.Commercial.DeviceFingerprint.Create(plan.Device));
}

public interface INvmeIdentifyReadOnlyReader
{
    NvmeIdentifyReadOnlyEvidence Read(StorageSanitizeDryRunResult plan);
}

/// <summary>
/// Read-only NVMe Identify Controller collector. It uses IOCTL_STORAGE_QUERY_PROPERTY with
/// StorageAdapterProtocolSpecificProperty/NVMeDataTypeIdentify/CNS=Controller. It never uses
/// IOCTL_STORAGE_PROTOCOL_COMMAND, Sanitize, Format, lock, write, or a disk-management command.
/// </summary>
public sealed class WindowsNvmeIdentifyReadOnlyReader : INvmeIdentifyReadOnlyReader
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageAdapterProtocolSpecificProperty = 49;
    private const int PropertyStandardQuery = 0;
    private const int ProtocolTypeNvme = 3;
    private const int NvmeDataTypeIdentify = 1;
    private const int NvmeIdentifyCnsController = 1;
    private const int PropertyQueryBytes = 8;
    private const int ProtocolSpecificBytes = 40;
    private const int IdentifyBytes = 4096;

    public NvmeIdentifyReadOnlyEvidence Read(StorageSanitizeDryRunResult plan)
    {
        var fingerprint = DeviceFingerprint.Create(plan.Device);
        if (!plan.Device.IsResolved || !string.Equals(plan.Device.BusType, "NVMe", StringComparison.OrdinalIgnoreCase))
            return Failed(fingerprint, "NVMe 물리 디스크가 아니거나 장치 식별이 완료되지 않았습니다.");
        if (!OperatingSystem.IsWindows()) return Failed(fingerprint, "NVMe Identify 읽기 전용 수집은 Windows에서만 지원합니다.");

        try
        {
            var data = QueryIdentifyController(plan.Device.DiskNumber!.Value);
            return NvmeIdentifyEvidenceParser.Parse(data, fingerprint, DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return Failed(fingerprint, "NVMe Identify Controller 읽기 전용 수집 실패: " + exception.Message);
        }
    }

    private static NvmeIdentifyReadOnlyEvidence Failed(string fingerprint, string detail) => new()
    {
        DeviceFingerprint = fingerprint, CollectedAtUtc = DateTimeOffset.UtcNow, ReadSucceeded = false, Detail = detail
    };

    private static byte[] QueryIdentifyController(int diskNumber)
    {
        var input = new byte[PropertyQueryBytes + ProtocolSpecificBytes];
        WriteInt(input, 0, StorageAdapterProtocolSpecificProperty);
        WriteInt(input, 4, PropertyStandardQuery);
        WriteInt(input, 8, ProtocolTypeNvme);
        WriteInt(input, 12, NvmeDataTypeIdentify);
        WriteInt(input, 16, NvmeIdentifyCnsController);
        WriteInt(input, 24, ProtocolSpecificBytes);
        WriteInt(input, 28, IdentifyBytes);
        var output = new byte[PropertyQueryBytes + ProtocolSpecificBytes + IdentifyBytes];

        using SafeFileHandle handle = File.OpenHandle("\\\\.\\PhysicalDrive" + diskNumber, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (!DeviceIoControl(handle, IoctlStorageQueryProperty, input, input.Length, output, output.Length, out var returned, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var dataStart = PropertyQueryBytes + ProtocolSpecificBytes;
        if (returned < dataStart + IdentifyBytes) throw new IOException("NVMe Identify 응답 길이가 부족합니다.");
        return output.AsSpan(dataStart, IdentifyBytes).ToArray();
    }

    private static void WriteInt(byte[] buffer, int offset, int value) => BitConverter.GetBytes(value).CopyTo(buffer, offset);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, byte[] input, int inputLength, byte[] output, int outputLength, out int bytesReturned, IntPtr overlapped);
}

public static class NvmeIdentifyEvidenceParser
{
    private const int IdentifyBytes = 4096;
    private const int SanitizeCapabilitiesOffset = 328;

    public static NvmeIdentifyReadOnlyEvidence Parse(ReadOnlySpan<byte> identifyController, string deviceFingerprint, DateTimeOffset collectedAtUtc)
    {
        if (identifyController.Length != IdentifyBytes)
            return new NvmeIdentifyReadOnlyEvidence
            {
                DeviceFingerprint = deviceFingerprint, CollectedAtUtc = collectedAtUtc,
                Detail = "NVMe Identify Controller 응답 길이가 4096바이트가 아닙니다. 증거로 사용하지 않습니다."
            };

        var sanicap = BitConverter.ToUInt32(identifyController.Slice(SanitizeCapabilitiesOffset, sizeof(uint)));
        return new NvmeIdentifyReadOnlyEvidence
        {
            DeviceFingerprint = deviceFingerprint,
            CollectedAtUtc = collectedAtUtc,
            ReadSucceeded = true,
            SupportsCryptoErase = (sanicap & 0b001) != 0,
            SupportsBlockErase = (sanicap & 0b010) != 0,
            SupportsOverwrite = (sanicap & 0b100) != 0,
            NoDeallocateInhibited = (sanicap & (1u << 30)) != 0,
            IdentifySha256 = Convert.ToHexString(SHA256.HashData(identifyController)).ToLowerInvariant(),
            Detail = "NVMe Identify Controller SANICAP을 읽기 전용으로 수집했습니다. 장치 실행 승인이나 삭제 완료 증명은 아닙니다."
        };
    }
}