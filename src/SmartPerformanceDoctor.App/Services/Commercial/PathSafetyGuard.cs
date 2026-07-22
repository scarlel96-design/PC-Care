using SmartPerformanceDoctor.App.Services.Security;

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

        if (full.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "네트워크·UNC·장치 경로는 파일 단위 보안 삭제를 보장할 수 없어 차단됩니다.");
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

        var protectedUserRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (protectedUserRoots.Any(rootPath => IsSamePath(full, rootPath)))
        {
            return (false, "사용자 프로필·바탕 화면·문서 전체 루트 삭제는 차단됩니다.");
        }

        var protectedProductTrees = new[]
        {
            RuntimePaths.InstallRoot,
            RuntimePaths.UserRoot,
            SecureVaultPaths.Root,
            Environment.CurrentDirectory
        };
        foreach (var protectedTree in protectedProductTrees)
        {
            if (IsSamePath(full, protectedTree) || IsUnderPath(full, protectedTree))
            {
                return (false, "PC 케어 설치·데이터·로그·금고·현재 작업 경로는 보안 삭제로 지울 수 없습니다.");
            }
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath)
            && IsSamePath(full, Environment.ProcessPath))
        {
            return (false, "현재 실행 파일은 보안 삭제할 수 없습니다.");
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

    private static bool IsSamePath(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd('\\', '/'),
                Path.GetFullPath(right).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUnderPath(string candidate, string? parent)
    {
        if (string.IsNullOrWhiteSpace(parent))
        {
            return false;
        }

        try
        {
            var normalizedCandidate = Path.GetFullPath(candidate);
            var normalizedParent = Path.GetFullPath(parent).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
                || first.Equals("ProgramData", StringComparison.OrdinalIgnoreCase)
                || first.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
                || first.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Recovery", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Boot", StringComparison.OrdinalIgnoreCase))
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
