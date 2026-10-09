using System.Diagnostics;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// Purge foundation only. This module identifies a physical disk and evaluates gates;
/// it never locks a volume, sends an IOCTL, invokes firmware commands, or writes a disk.
/// </summary>
public interface IStorageSanitizer
{
    StorageSanitizeDryRunResult PlanDryRun(string rootPath);
}

public interface IStorageDeviceProbe
{
    StorageDeviceIdentity Probe(string rootPath);
}

public enum StorageSanitizeMethod
{
    CryptoErase,
    BlockErase,
    Overwrite,
    BitLockerKeyDestroy
}

public sealed class StorageDeviceIdentity
{
    public string RootPath { get; init; } = "";
    public string DriveLetter { get; init; } = "";
    public int? DiskNumber { get; init; }
    public string UniqueId { get; init; } = "";
    public string SerialNumber { get; init; } = "";
    public string Model { get; init; } = "";
    public string BusType { get; init; } = "Unknown";
    public string FileSystem { get; init; } = "Unknown";
    public string HealthStatus { get; init; } = "Unknown";
    public string OperationalStatus { get; init; } = "Unknown";
    public long SizeBytes { get; init; }
    public bool IsBootDisk { get; init; }
    public bool IsSystemDisk { get; init; }
    public bool IsOffline { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsResolved => DiskNumber.HasValue && !string.IsNullOrWhiteSpace(UniqueId);
    public string DisplayId => IsResolved ? $"Disk {DiskNumber} · {Model} · {SizeBytes / 1024d / 1024 / 1024:0.0} GB" : "물리 디스크 식별 실패";
}

public sealed class StorageSanitizeCapability
{
    public string Code { get; init; } = "";
    public bool Supported { get; init; }
    public string Detail { get; init; } = "";
}

public sealed class StorageSanitizeDryRunResult
{
    public string OperationId { get; init; } = "";
    public string RootPath { get; init; } = "";
    public StorageDeviceIdentity Device { get; init; } = new();
    public bool IsSystemDisk { get; init; }
    public bool RequiresWinPe { get; init; }
    public bool IsWinPe { get; init; }
    public bool RequiresElevation { get; init; } = true;
    public bool ExecutionEnabled => false;
    public string AuditEvent { get; init; } = "PURGE_DRY_RUN_ONLY";
    public string SafetyMessage { get; init; } = "장치 Purge는 준비 단계입니다. 이 버전에서는 실행하지 않습니다.";
    public IReadOnlyList<StorageSanitizeCapability> Capabilities { get; init; } = Array.Empty<StorageSanitizeCapability>();
    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();
}

/// <summary>Read-only Windows disk inventory through PowerShell CIM/Storage cmdlets.</summary>
public sealed class WindowsStorageDeviceProbe : IStorageDeviceProbe
{
    public StorageDeviceIdentity Probe(string rootPath)
    {
        var root = NormalizeRoot(rootPath);
        var letter = root.TrimEnd('\\')[0].ToString().ToUpperInvariant();
        try
        {
            var script = "$ErrorActionPreference='Stop'; $p=Get-Partition -DriveLetter '" + letter + "'; $d=$p | Get-Disk; $v=Get-Volume -DriveLetter '" + letter + "'; [pscustomobject]@{Number=$d.Number;UniqueId=$d.UniqueId;SerialNumber=$d.SerialNumber;Model=$d.FriendlyName;BusType=$d.BusType;FileSystem=$v.FileSystem;HealthStatus=$d.HealthStatus;OperationalStatus=($d.OperationalStatus -join ',');Size=$d.Size;IsBoot=$d.IsBoot;IsSystem=$d.IsSystem;IsOffline=$d.IsOffline;IsReadOnly=$d.IsReadOnly} | ConvertTo-Json -Compress";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null) return new StorageDeviceIdentity { RootPath = root, DriveLetter = letter };
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(6000)) { try { process.Kill(true); } catch { } return new StorageDeviceIdentity { RootPath = root, DriveLetter = letter }; }
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) return new StorageDeviceIdentity { RootPath = root, DriveLetter = letter };
            using var document = JsonDocument.Parse(output);
            var value = document.RootElement;
            return new StorageDeviceIdentity
            {
                RootPath = root, DriveLetter = letter,
                DiskNumber = TryInt(value, "Number"), UniqueId = Get(value, "UniqueId"), SerialNumber = Get(value, "SerialNumber"),
                Model = Get(value, "Model"), BusType = Get(value, "BusType", "Unknown"), FileSystem = Get(value, "FileSystem", "Unknown"),
                HealthStatus = Get(value, "HealthStatus", "Unknown"), OperationalStatus = Get(value, "OperationalStatus", "Unknown"),
                SizeBytes = TryLong(value, "Size"), IsBootDisk = TryBool(value, "IsBoot"), IsSystemDisk = TryBool(value, "IsSystem"),
                IsOffline = TryBool(value, "IsOffline"), IsReadOnly = TryBool(value, "IsReadOnly")
            };
        }
        catch { return new StorageDeviceIdentity { RootPath = root, DriveLetter = letter }; }
    }

    private static string NormalizeRoot(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(root) || root.Length < 3 || !char.IsLetter(root[0])) throw new ArgumentException("로컬 드라이브 루트만 장치 Purge Dry Run 대상으로 지정할 수 있습니다.", nameof(path));
        return root;
    }
    private static string Get(JsonElement value, string name, string fallback = "") => value.TryGetProperty(name, out var property) && property.ValueKind is not JsonValueKind.Null ? property.ToString() : fallback;
    private static int? TryInt(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.TryGetInt32(out var number) ? number : null;
    private static long TryLong(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.TryGetInt64(out var number) ? number : 0;
    private static bool TryBool(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False && property.GetBoolean();
}

public sealed class StorageSanitizerDryRun : IStorageSanitizer
{
    private readonly IStorageDeviceProbe _probe;
    public StorageSanitizerDryRun(IStorageDeviceProbe? probe = null) => _probe = probe ?? new WindowsStorageDeviceProbe();

    public StorageSanitizeDryRunResult PlanDryRun(string rootPath)
    {
        var device = _probe.Probe(rootPath);
        var isWinPe = IsWinPe();
        var elevated = ProcessElevationService.IsAdministrator();
        var isRemovable = string.Equals(device.BusType, "USB", StringComparison.OrdinalIgnoreCase) || string.Equals(device.BusType, "SD", StringComparison.OrdinalIgnoreCase);
        var requiresWinPe = device.IsBootDisk || device.IsSystemDisk;
        var supportedOs = Environment.OSVersion.Version.Build >= 20348;
        var blocks = new List<string>();
        if (!device.IsResolved) blocks.Add("물리 디스크 식별에 실패했습니다.");
        if ((device.IsBootDisk || device.IsSystemDisk) && !isWinPe) blocks.Add("부팅/시스템 디스크는 일반 Windows에서 Purge할 수 없습니다.");
        if (isRemovable) blocks.Add("USB/SD 외장 매체는 장치 식별·전원 안정성 검증 전까지 차단합니다.");
        if (device.IsOffline || device.IsReadOnly) blocks.Add("오프라인 또는 읽기 전용 디스크입니다.");
        if (!elevated) blocks.Add("향후 Purge는 관리자 권한이 필요합니다.");
        var capabilities = new List<StorageSanitizeCapability>
        {
            new() { Code="CAP_ROOT_LOCAL", Supported=true, Detail=$"대상 볼륨: {device.RootPath}" },
            new() { Code="CAP_PHYSICAL_IDENTITY", Supported=device.IsResolved, Detail=device.IsResolved ? $"{device.DisplayId} · ID {Short(device.UniqueId)}" : "Disk 번호와 고유 ID를 모두 확인하지 못했습니다." },
            new() { Code="CAP_SYSTEM_BOOT_GATE", Supported=!(device.IsBootDisk || device.IsSystemDisk), Detail=requiresWinPe ? "부팅/시스템 디스크: 향후에도 WinPE에서만 진행 가능" : "데이터 디스크 후보" },
            new() { Code="CAP_WINPE", Supported=isWinPe, Detail=isWinPe ? "WinPE 환경 감지" : "일반 Windows 환경: 부팅 디스크 Purge는 차단" },
            new() { Code="CAP_ELEVATION", Supported=elevated, Detail=elevated ? "관리자 권한 확인" : "Dry Run은 가능하나 실행 권한 없음" },
            new() { Code="CAP_OS_REINITIALIZE_MEDIA", Supported=supportedOs, Detail=supportedOs ? "Windows API 지원 버전 후보 (명령 실행·보증 아님)" : "Windows 빌드가 요구 조건보다 낮음" },
            new() { Code="CAP_NVME_SANITIZE_CANDIDATE", Supported=string.Equals(device.BusType,"NVMe",StringComparison.OrdinalIgnoreCase), Detail=string.Equals(device.BusType,"NVMe",StringComparison.OrdinalIgnoreCase) ? "NVMe Sanitize 후보: 차기 단계에서 Identify/지원 효과 로그 검증 필요" : "NVMe 디스크 아님" },
            new() { Code="CAP_EXTERNAL_MEDIA_GATE", Supported=!isRemovable, Detail=isRemovable ? "외장 매체 차단" : "내장/비-USB 매체" },
            new() { Code="CAP_EXECUTION_DISABLED", Supported=true, Detail="안전장치: 이 단계는 IOCTL·Sanitize·Format·잠금 명령을 호출하지 않습니다." }
        };
        return new StorageSanitizeDryRunResult
        {
            OperationId = "purge-dryrun-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"), RootPath = device.RootPath, Device = device,
            IsSystemDisk = device.IsSystemDisk || device.IsBootDisk, RequiresWinPe = requiresWinPe, IsWinPe = isWinPe,
            SafetyMessage = blocks.Count == 0 ? "장치 식별과 안전 게이트를 통과한 실행 후보입니다. 하지만 이 버전에서는 어떤 Purge 명령도 실행하지 않습니다." : "실행 후보가 차단되었습니다. " + string.Join(" ", blocks),
            Capabilities = capabilities, BlockingReasons = blocks
        };
    }

    private static bool IsWinPe()
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        return !string.IsNullOrWhiteSpace(systemRoot) && File.Exists(Path.Combine(systemRoot, "System32", "wpeutil.exe"));
    }
    private static string Short(string value) => value.Length <= 16 ? value : value[..8] + "…" + value[^6..];
}