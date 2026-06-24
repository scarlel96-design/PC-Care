namespace SmartPerformanceDoctor.App.Services.Commercial;

public static class PathSafetyGuard
{
    private static readonly string[] BlockedPrefixes =
    [
        @"C:\Windows",
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"C:\ProgramData\Microsoft",
        "System Volume Information",
        "WinSxS",
        "WindowsApps"
    ];

    public static (bool Allowed, string Reason) Evaluate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "경로가 비어 있습니다.");
        }

        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "";
        if (string.Equals(full.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "드라이브 루트 삭제는 차단됩니다.");
        }

        foreach (var blocked in BlockedPrefixes)
        {
            if (full.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "시스템 보호 경로입니다.");
            }
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.Equals(full.TrimEnd('\\'), profile.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "사용자 프로필 전체 삭제는 차단됩니다.");
        }

        return (true, "");
    }
}