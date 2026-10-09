namespace SmartPerformanceDoctor.App.Services.Commercial;

public enum SecureDeleteSecurityLevel
{
    Standard,
    Professional,
    Maximum
}

/// <summary>Storage-aware policy selection. Unknown media is deliberately handled conservatively.</summary>
public static class SecureDeleteStorageProfiler
{
    public static string Profile(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root)) return "Unknown";
            var drive = new DriveInfo(root);
            if (drive.DriveType == DriveType.Removable)
                return drive.DriveFormat.Contains("NTFS", StringComparison.OrdinalIgnoreCase) ? "USB-NTFS" : "USB-FAT";
            if (drive.DriveType != DriveType.Fixed) return "Unknown";
            return QueryPhysicalMediaType(root);
        }
        catch { return "Unknown"; }
    }

    public static string SelectProtocol(string storage, SecureDeleteSecurityLevel level) => (storage, level) switch
    {
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Maximum) => "file-clear.hdd.csprng-7pass.sample-verify.v7",
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Professional) => "file-clear.hdd.csprng-3pass.sample-verify.v7",
        ("HDD" or "USB-NTFS", _) => "file-clear.hdd.csprng-1pass.sample-verify.v7",
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Maximum) => "file-clear.ssd.csprng-3pass.sample-verify.trim.v7",
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Professional) => "file-clear.ssd.csprng-2pass.sample-verify.trim.v7",
        ("SSD" or "NVMe", _) => "file-clear.ssd.csprng-1pass.sample-verify.trim.v7",
        _ => "file-clear.conservative.csprng-1pass.sample-verify.v7"
    };

    // This is an implementation-complexity meter, never a recovery guarantee or certification.
    public static int GetTechnicalDeletionIntensity(string storage, SecureDeleteSecurityLevel level) => level switch
    {
        SecureDeleteSecurityLevel.Maximum => 3,
        SecureDeleteSecurityLevel.Professional => 2,
        _ => 1
    };

    // File-only operations are not certified sanitization. Keep legacy API safe for callers.
    public static int EstimateCertifiedResistanceLevel(string storage, SecureDeleteSecurityLevel level) => 0;
    public static bool IsLevel5Certified(string storage, SecureDeleteSecurityLevel level, string deleteScope = "file") => false;
    public static string BuildCertifiedResistanceLabel(string storage, SecureDeleteSecurityLevel level) => "인증 아님 · 파일 단위 Clear";

    public static string BuildResistanceDisclaimer(string storage, SecureDeleteSecurityLevel level) => storage switch
    {
        "SSD" or "NVMe" => "SSD/NVMe 파일 단위 Clear는 논리 영역에 대한 덮어쓰기·표본 검증·TRIM 요청입니다. 웨어 레벨링, 오버프로비저닝, 스냅샷·클라우드 사본 때문에 물리 셀 삭제나 복구 불가를 보장하지 않습니다.",
        "HDD" or "USB-NTFS" => "HDD 파일 단위 Clear는 파일의 논리 영역을 다중 덮어쓰기하고 표본 검증합니다. 섀도 복사본·백업·다른 하드링크는 이 작업 범위 밖입니다.",
        _ => "저장장치 유형을 신뢰성 있게 식별하지 못했습니다. 보수적인 1회 CSPRNG 덮어쓰기와 표본 검증만 계획하며, 복구 불가를 보장하지 않습니다."
    };

    public static int EstimateResistanceLevel(string storage, SecureDeleteSecurityLevel level) => 0;

    public static int GetOverwritePasses(string storage, SecureDeleteSecurityLevel level) => (storage, level) switch
    {
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Maximum) => 7,
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Professional) => 3,
        ("HDD" or "USB-NTFS", _) => 1,
        ("SSD" or "NVMe", _) => 0,
        _ => 1
    };

    public static int GetFileClearPasses(string storage, SecureDeleteSecurityLevel level) => storage is "SSD" or "NVMe"
        ? level switch { SecureDeleteSecurityLevel.Maximum => 3, SecureDeleteSecurityLevel.Professional => 2, _ => 1 }
        : Math.Max(1, GetOverwritePasses(storage, level));


    private static string QueryPhysicalMediaType(string root)
    {
        try
        {
            var letter = root.TrimEnd('\\').TrimEnd(':');
            if (letter.Length != 1) return "Unknown";
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"$p=Get-Partition -DriveLetter '{letter}' -ErrorAction Stop; $d=$p | Get-Disk | Get-PhysicalDisk -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty MediaType; $d\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null) return "Unknown";
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(5000)) { try { process.Kill(true); } catch { } return "Unknown"; }
            return output.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ? "NVMe"
                : output.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD"
                : output.Contains("HDD", StringComparison.OrdinalIgnoreCase) ? "HDD"
                : "Unknown";
        }
        catch { return "Unknown"; }
    }
}
