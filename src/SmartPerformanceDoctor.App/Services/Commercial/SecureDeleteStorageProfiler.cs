namespace SmartPerformanceDoctor.App.Services.Commercial;

public enum SecureDeleteSecurityLevel
{
    Standard,
    Professional,
    Maximum
}

public static class SecureDeleteStorageProfiler
{
    public static string Profile(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
            var drive = new DriveInfo(root);
            if (drive.DriveType == DriveType.Removable)
            {
                return drive.DriveFormat.Contains("NTFS", StringComparison.OrdinalIgnoreCase) ? "USB-NTFS" : "USB-FAT";
            }

            if (drive.DriveType != DriveType.Fixed)
            {
                return "Unknown";
            }

            var media = QueryPhysicalMediaType(root);
            return media switch
            {
                "SSD" => "SSD",
                "HDD" => "HDD",
                _ when string.Equals(root, "C:\\", StringComparison.OrdinalIgnoreCase) => "SSD",
                _ => "HDD"
            };
        }
        catch
        {
            return "Unknown";
        }
    }

    public static string SelectProtocol(string storage, SecureDeleteSecurityLevel level) => (storage, level) switch
    {
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Maximum) => "hdd.seven_pass.dod_ext.v2",
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Professional) => "hdd.three_pass.dod.v2",
        ("HDD" or "USB-NTFS", _) => "hdd.single_pass.random.v2",
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Maximum) => "ssd.astra_shred_obfuscate_10x.random_4x.trim.retrim.zero.v5",
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Professional) => "ssd.astra_shred_obfuscate_7x.random_2x.trim.retrim.zero.v5",
        ("SSD" or "NVMe", _) => "ssd.astra_shred_obfuscate_4x.trim.retrim.v5",
        _ => level == SecureDeleteSecurityLevel.Standard
            ? "standard.secure_delete.v2"
            : "storage.adaptive.best_effort.v2"
    };

    /// <summary>
    /// 실제 수행되는 삭제 강도(기술 티어). SSD/NVMe에서도 Maximum이면 최대 체인을 적용합니다.
    /// </summary>
    public static int GetTechnicalDeletionIntensity(string storage, SecureDeleteSecurityLevel level) => (storage, level) switch
    {
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Maximum) => 5,
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Professional) => 4,
        ("HDD" or "USB-NTFS", _) => 3,
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Maximum) => 5,
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Professional) => 4,
        ("SSD" or "NVMe", _) => 3,
        _ => 2
    };

    /// <summary>
    /// 사용자에게 표시하는 공인 복구 저항 등급. SSD/NVMe 파일 단위는 Level 5 보증을 표기하지 않습니다.
    /// </summary>
    public static int EstimateCertifiedResistanceLevel(string storage, SecureDeleteSecurityLevel level) => (storage, level) switch
    {
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Maximum) => 5,
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Professional) => 4,
        ("HDD" or "USB-NTFS", _) => 3,
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Maximum) => 3,
        ("SSD" or "NVMe", SecureDeleteSecurityLevel.Professional) => 3,
        ("SSD" or "NVMe", _) => 2,
        _ => 1
    };

    public static bool IsLevel5Certified(string storage, SecureDeleteSecurityLevel level, string deleteScope = "file") =>
        string.Equals(deleteScope, "file", StringComparison.OrdinalIgnoreCase)
            ? storage is "HDD" or "USB-NTFS" && level == SecureDeleteSecurityLevel.Maximum
            : false;

    public static string BuildCertifiedResistanceLabel(string storage, SecureDeleteSecurityLevel level)
    {
        var certified = EstimateCertifiedResistanceLevel(storage, level);
        var intensity = GetTechnicalDeletionIntensity(storage, level);
        if (storage is "SSD" or "NVMe" && intensity > certified)
        {
            return $"Level {certified} (storage-dependent) · 기술 강도 Tier {intensity}";
        }

        return $"Level {certified}";
    }

    public static string BuildResistanceDisclaimer(string storage, SecureDeleteSecurityLevel level)
    {
        if (storage is not ("SSD" or "NVMe"))
        {
            return "";
        }

        return level switch
        {
            SecureDeleteSecurityLevel.Maximum =>
                "SSD/NVMe 파일 단위: 최대 강도 삭제 체인(난독화·랜덤 덮어쓰기·TRIM·볼륨 retrim)을 적용합니다. " +
                "다만 저장장치 물리·섀도 복사본·클라우드 동기화 등으로 잔존 가능성이 있어 공인 복구 저항 등급은 Level 5가 아닙니다.",
            SecureDeleteSecurityLevel.Professional =>
                "SSD/NVMe 파일 단위: 전문가급 삭제 체인을 적용하나, 공인 복구 저항 등급은 storage-dependent Level 3 이하로 표시됩니다.",
            _ =>
                "SSD/NVMe: TRIM·retrim 중심 삭제. 공인 등급은 storage-dependent입니다."
        };
    }

    public static int EstimateResistanceLevel(string storage, SecureDeleteSecurityLevel level) =>
        EstimateCertifiedResistanceLevel(storage, level);

    public static int GetOverwritePasses(string storage, SecureDeleteSecurityLevel level) => (storage, level) switch
    {
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Maximum) => 7,
        ("HDD" or "USB-NTFS", SecureDeleteSecurityLevel.Professional) => 3,
        ("HDD" or "USB-NTFS", _) => 1,
        _ => 0
    };

    public static int GetSsdObfuscationPasses(SecureDeleteSecurityLevel level) => level switch
    {
        SecureDeleteSecurityLevel.Maximum => 10,
        SecureDeleteSecurityLevel.Professional => 7,
        _ => 4
    };

    public static int GetSsdCryptoErasePasses(SecureDeleteSecurityLevel level) => level switch
    {
        SecureDeleteSecurityLevel.Maximum => 4,
        SecureDeleteSecurityLevel.Professional => 2,
        _ => 1
    };

    private static string QueryPhysicalMediaType(string root)
    {
        try
        {
            var letter = root.TrimEnd('\\', ':');
            if (letter.Length == 0)
            {
                return "Unknown";
            }

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"(Get-PhysicalDisk | Get-Partition | Where-Object DriveLetter -eq '{letter}' | Select-Object -First 1 | Get-PhysicalDisk).MediaType\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return "Unknown";
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return output.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD"
                : output.Contains("HDD", StringComparison.OrdinalIgnoreCase) ? "HDD"
                : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}