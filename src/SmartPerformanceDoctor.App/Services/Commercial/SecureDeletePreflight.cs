using SmartPerformanceDoctor.App.Models.Commercial;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>Non-destructive P01–P11 gate used by every secure-delete dry run.</summary>
internal static class SecureDeletePreflight
{
    public static SecureDeletePreflightReport Inspect(
        string path,
        string storageType,
        string fileSystem,
        SecureDeleteSecurityLevel level,
        bool pathAllowed,
        string pathReason)
    {
        var checks = new List<SecureDeletePreflightCheck>();
        Add(checks, "P01", pathAllowed ? "pass" : "fail", pathAllowed ? "보호 경로 정책 통과" : pathReason);

        var existsAsFile = File.Exists(path);
        var attributes = TryGetAttributes(path);
        Add(checks, "P06", attributes.HasValue && attributes.Value.HasFlag(FileAttributes.ReparsePoint) ? "fail" : "pass",
            attributes.HasValue && attributes.Value.HasFlag(FileAttributes.ReparsePoint)
                ? "재분석 지점·심볼릭 링크는 대상 링크를 따라가지 않으므로 차단했습니다."
                : "재분석 지점 아님");

        if (existsAsFile)
        {
            var locked = !CanOpenExclusively(path);
            Add(checks, "P02", locked ? "fail" : "pass", locked ? "다른 프로세스가 파일을 사용 중입니다." : "배타적 열기 가능");
            var readOnly = attributes.HasValue && attributes.Value.HasFlag(FileAttributes.ReadOnly);
            Add(checks, "P03", readOnly ? "warn" : "pass", readOnly ? "읽기 전용 속성은 실행 시 해제됩니다." : "읽기 전용 아님");
            Add(checks, "P05", "warn", "하드링크는 실행 전 별도 경로 확인이 필요합니다. 파일 삭제 후 연결 경로가 남을 수 있습니다.");
        }
        else
        {
            Add(checks, "P02", "warn", "폴더는 포함 파일별로 잠금 상태를 다시 검사합니다.");
            Add(checks, "P03", "pass", "폴더 속성 확인 완료");
            Add(checks, "P05", "pass", "폴더 대상은 하드링크를 사용하지 않습니다.");
        }

        Add(checks, "P04", "pass", fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase)
            ? "NTFS ADS 열거·정리 대상" : "NTFS가 아니므로 ADS를 지원하지 않을 수 있습니다.");
        Add(checks, "P07", storageType == "Unknown" ? "warn" : "pass", $"저장장치 유형: {storageType}");
        Add(checks, "P08", fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? "pass" : "warn",
            $"파일 시스템: {fileSystem}");

        var shadow = ForensicSecureDeleteEngine.ScanShadowCopyRisk(path);
        Add(checks, "P09", shadow.HasRisk ? "warn" : "pass", shadow.HasRisk
            ? "섀도 복사본이 감지되었습니다. 현재 파일 삭제는 VSS를 자동 삭제하지 않습니다."
            : "섀도 복사본 위험 미감지");

        var special = attributes.HasValue && (attributes.Value.HasFlag(FileAttributes.Compressed)
            || attributes.Value.HasFlag(FileAttributes.Encrypted)
            || attributes.Value.HasFlag(FileAttributes.SparseFile));
        Add(checks, "P10", special ? "warn" : "pass", special
            ? "압축·EFS·희소 파일은 일반 파일과 다른 할당 특성을 가집니다."
            : "특수 할당 속성 미감지");

        var freeBytes = TryGetAvailableFreeSpace(path);
        Add(checks, "P11", freeBytes >= 64L * 1024 * 1024 ? "pass" : "warn",
            freeBytes >= 64L * 1024 * 1024 ? "동일 볼륨 작업 공간 확인" : "여유 공간이 낮아 검증 범위가 제한될 수 있습니다.");

        var elevated = ProcessElevationService.IsAdministrator();
        Add(checks, "E_ELEVATION_REQUIRED", level == SecureDeleteSecurityLevel.Maximum && !elevated ? "fail" : "pass",
            level == SecureDeleteSecurityLevel.Maximum && !elevated
                ? "강화 포렌식 수준은 관리자 권한으로 다시 실행해야 합니다."
                : elevated ? "관리자 권한 확인" : "전문가 수준은 표준 권한으로 실행 가능");

        return new SecureDeletePreflightReport
        {
            Checks = checks,
            FileSystem = fileSystem,
            StorageType = storageType,
            IsAdministrator = elevated,
            BitLockerProtected = TryDetectBitLocker(path),
            HasShadowCopies = shadow.HasRisk
        };
    }

    private static void Add(List<SecureDeletePreflightCheck> checks, string code, string status, string detail) =>
        checks.Add(new SecureDeletePreflightCheck { Code = code, Status = status, Detail = detail });

    private static FileAttributes? TryGetAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch { return null; }
    }

    private static bool CanOpenExclusively(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch { return false; }
    }

    private static long TryGetAvailableFreeSpace(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace; }
        catch { return 0; }
    }

    private static bool TryDetectBitLocker(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root)) return false;
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "manage-bde.exe",
                Arguments = $"-status {root.TrimEnd('\\')}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null) return false;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(4000);
            return output.Contains("Protection On", StringComparison.OrdinalIgnoreCase)
                   || output.Contains("보호 상태:    켜짐", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
