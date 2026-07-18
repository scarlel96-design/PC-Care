namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>Path safety for shred/migration/export targets (Windows-focused).</summary>
public static class LabSecurePath
{
    public static (bool Allowed, string Reason) Evaluate(string path, string? vaultRootToProtect = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "경로 비어 있음");
        }

        // UNC / network root: refuse destructive ops (remote replica risk + policy)
        var trimmed = path.Trim();
        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return (false, "네트워크(UNC) 경로 금지");
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch
        {
            return (false, "경로 해석 실패");
        }

        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return (false, "네트워크(UNC) 경로 금지");
        }

        var root = Path.GetPathRoot(full) ?? "";
        if (string.Equals(full.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "드라이브 루트 금지");
        }

        if (ShredNext.LabShredPolicy.IsSystemPathBlocked(full))
        {
            return (false, "시스템 보호 경로");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile)
            && string.Equals(full.TrimEnd('\\'), profile.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "사용자 프로필 루트 금지");
        }

        // Desktop / Documents / Downloads roots themselves (not children) — accidental wipe
        foreach (var special in new[]
                 {
                     Environment.SpecialFolder.DesktopDirectory,
                     Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.MyPictures
                 })
        {
            var sp = Environment.GetFolderPath(special);
            if (!string.IsNullOrWhiteSpace(sp)
                && string.Equals(full.TrimEnd('\\'), sp.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                return (false, "특수 폴더 루트 금지 (하위 파일은 허용)");
            }
        }

        if (!string.IsNullOrWhiteSpace(vaultRootToProtect))
        {
            var v = Path.GetFullPath(vaultRootToProtect).TrimEnd('\\') + "\\";
            var f = full.TrimEnd('\\') + "\\";
            if (f.StartsWith(v, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "활성 금고 저장소 경로 금지");
            }
        }

        try
        {
            if (Directory.Exists(full) || File.Exists(full))
            {
                var attrs = File.GetAttributes(full);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    return (false, "재분석 지점(정션/심볼릭) 금지");
                }
            }

            // Refuse shred of directories that look like whole user trees when path ends with Downloads
            if (Directory.Exists(full))
            {
                var name = Path.GetFileName(full.TrimEnd('\\'));
                if (name.Equals("Downloads", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("AppData", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "민감 디렉터리 루트 금지");
                }
            }
        }
        catch
        {
            return (false, "속성 확인 실패");
        }

        return (true, "");
    }

    /// <summary>Export destination must not be system/vault/UNC.</summary>
    public static (bool Allowed, string Reason) EvaluateExportDirectory(string destDirectory, string? vaultRoot = null)
    {
        if (string.IsNullOrWhiteSpace(destDirectory))
        {
            return (false, "내보내기 경로 비어 있음");
        }

        string full;
        try
        {
            full = Path.GetFullPath(destDirectory);
        }
        catch
        {
            return (false, "내보내기 경로 해석 실패");
        }

        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return (false, "네트워크(UNC) 내보내기 금지");
        }

        if (ShredNext.LabShredPolicy.IsSystemPathBlocked(full))
        {
            return (false, "시스템 경로로 내보내기 금지");
        }

        if (!string.IsNullOrWhiteSpace(vaultRoot))
        {
            var v = Path.GetFullPath(vaultRoot).TrimEnd('\\') + "\\";
            var f = full.TrimEnd('\\') + "\\";
            if (f.StartsWith(v, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "금고 내부로 내보내기 금지");
            }
        }

        return (true, "");
    }

    public static bool IsCloudSyncPath(string fullPath) =>
        fullPath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)
        || fullPath.Contains("Google Drive", StringComparison.OrdinalIgnoreCase)
        || fullPath.Contains("Dropbox", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetworkPath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal));
}
