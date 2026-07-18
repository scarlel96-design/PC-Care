namespace SmartPerformanceDoctor.App.Services.Commercial;

public static class PathSafetyGuard
{
    public static (bool Allowed, string Reason) Evaluate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "경로가 비어 있습니다.");
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch
        {
            return (false, "경로를 해석할 수 없습니다.");
        }

        var root = Path.GetPathRoot(full) ?? "";
        if (string.Equals(full.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "드라이브 루트 삭제는 차단됩니다.");
        }

        if (IsUnderBlockedSystemTree(full))
        {
            return (false, "시스템 보호 경로입니다.");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile)
            && string.Equals(full.TrimEnd('\\'), profile.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "사용자 프로필 전체 삭제는 차단됩니다.");
        }

        // Never allow shredding the live vault store itself.
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApp))
        {
            var vaultRoot = Path.Combine(localApp, "SmartPerformanceDoctor", "secure_vault");
            if (full.StartsWith(vaultRoot, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "보안 금고 저장소 경로는 보안 삭제로 지울 수 없습니다.");
            }
        }

        try
        {
            if (Directory.Exists(full))
            {
                var attrs = File.GetAttributes(full);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    return (false, "재분석 지점(정션/심볼릭 링크) 폴더는 차단됩니다.");
                }
            }
        }
        catch
        {
            // Path may be inaccessible — treat as blocked for fail-closed delete planning.
            return (false, "경로 속성을 확인할 수 없어 차단했습니다.");
        }

        return (true, "");
    }

    private static bool IsUnderBlockedSystemTree(string fullPath)
    {
        // Drive-agnostic critical roots: X:\Windows, X:\Program Files, ...
        var segments = fullPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            var first = segments[1];
            if (first.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Program Files", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase)
                || first.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
                || first.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Recovery", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Boot", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // ProgramData\Microsoft is high-risk; other ProgramData app caches may be user-requested.
            if (first.Equals("ProgramData", StringComparison.OrdinalIgnoreCase)
                && segments.Length >= 3
                && segments[2].Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (fullPath.Contains(@"\Windows\System32", StringComparison.OrdinalIgnoreCase)
            || fullPath.Contains(@"\Windows\SysWOW64", StringComparison.OrdinalIgnoreCase)
            || fullPath.Contains(@"\Windows\WinSxS", StringComparison.OrdinalIgnoreCase)
            || fullPath.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)
            || fullPath.Contains(@"\WinSxS\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
