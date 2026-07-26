using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// PC-L6 foundation only. This module never sends a sanitize command or modifies a disk.
/// Device purge must remain a separate elevated workflow from file secure-delete.
/// </summary>
public interface IStorageSanitizer
{
    StorageSanitizeDryRunResult PlanDryRun(string rootPath);
}

public enum StorageSanitizeMethod
{
    CryptoErase,
    BlockErase,
    Overwrite,
    BitLockerKeyDestroy
}

public sealed class StorageSanitizeCapability
{
    public string Code { get; init; } = "";
    public bool Supported { get; init; }
    public string Detail { get; init; } = "";
}

public sealed class StorageSanitizeDryRunResult
{
    public string RootPath { get; init; } = "";
    public bool IsSystemDisk { get; init; }
    public bool RequiresElevation { get; init; } = true;
    public bool ExecutionEnabled => false;
    public string AuditEvent { get; init; } = "SANITIZE_PLANNED";
    public string SafetyMessage { get; init; } = "PC-L6 장치 Purge는 준비 단계입니다. 이 버전에서는 실행하지 않습니다.";
    public IReadOnlyList<StorageSanitizeCapability> Capabilities { get; init; } = Array.Empty<StorageSanitizeCapability>();
}

public sealed class StorageSanitizerDryRun : IStorageSanitizer
{
    public StorageSanitizeDryRunResult PlanDryRun(string rootPath)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(rootPath)) ?? rootPath;
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var isSystemDisk = string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase);
        var storage = SecureDeleteStorageProfiler.Profile(root);
        var capabilities = new List<StorageSanitizeCapability>
        {
            new() { Code = "CAP_SYSTEM_DISK", Supported = isSystemDisk, Detail = isSystemDisk ? "시스템 디스크는 기본 차단됩니다." : "시스템 디스크 아님" },
            new() { Code = "CAP_REMOVABLE", Supported = new DriveInfo(root).DriveType == DriveType.Removable, Detail = "외장 매체 여부는 dry-run으로만 표시합니다." },
            new() { Code = "CAP_NVME_SANITIZE_CRYPTO", Supported = false, Detail = storage == "NVMe" ? "장치 식별·권한 helper 구현 전까지 실행 불가" : "NVMe 확인 필요" },
            new() { Code = "CAP_NVME_SANITIZE_BLOCK", Supported = false, Detail = "장치 단위 Block Erase는 이 버전에서 실행하지 않습니다." },
            new() { Code = "CAP_BITLOCKER_ON", Supported = false, Detail = "BitLocker 키 폐기는 별도 마법사와 이중 확인이 필요합니다." },
            new() { Code = "CAP_EXECUTION_DISABLED", Supported = true, Detail = "안전장치: 실제 Purge 명령은 호출하지 않습니다." }
        };

        return new StorageSanitizeDryRunResult
        {
            RootPath = root,
            IsSystemDisk = isSystemDisk,
            SafetyMessage = isSystemDisk
                ? "OS 디스크는 PC-L6 Purge 대상으로 사용할 수 없습니다. Windows RE 또는 부팅 미디어 절차가 필요합니다."
                : "PC-L6은 현재 지원 여부 확인과 dry-run만 제공합니다. 실제 장치 삭제는 아직 비활성화되어 있습니다.",
            Capabilities = capabilities
        };
    }
}
