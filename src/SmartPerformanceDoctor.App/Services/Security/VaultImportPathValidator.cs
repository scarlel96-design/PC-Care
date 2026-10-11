namespace SmartPerformanceDoctor.App.Services.Security;

public static class VaultImportPathValidator
{
    public static (bool Allowed, string NormalizedPath, string Message) ValidateFile(string path) =>
        Validate(path, expectDirectory: false);

    public static (bool Allowed, string NormalizedPath, string Message) ValidateDirectory(string path) =>
        Validate(path, expectDirectory: true);

    public static string[] EnumerateValidatedFiles(string root, CancellationToken cancellationToken = default)
    {
        var files = new List<string>();
        var directories = new Stack<string>();
        directories.Push(Path.GetFullPath(root));
        var visited = 0;
        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = directories.Pop();
            var check = ValidateDirectory(directory);
            if (!check.Allowed) throw new IOException(check.Message);
            foreach (var member in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++visited > 100_000) throw new IOException("폴더 항목이 검사 한도를 초과했습니다. 나누어 가져오세요.");
                var attributes = File.GetAttributes(member);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("폴더에 링크가 있어 가져오기를 중단했습니다.");
                if ((attributes & FileAttributes.Directory) != 0) directories.Push(member);
                else
                {
                    var file = ValidateFile(member);
                    if (!file.Allowed) throw new IOException(file.Message);
                    files.Add(file.NormalizedPath);
                }
            }
        }
        return files.ToArray();
    }

    private static (bool Allowed, string NormalizedPath, string Message) Validate(string path, bool expectDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, string.Empty, "선택 경로가 비어 있습니다.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return (false, string.Empty, "선택 경로를 해석할 수 없습니다.");
        }

        if (expectDirectory ? !Directory.Exists(fullPath) : !File.Exists(fullPath))
        {
            return (false, fullPath, expectDirectory
                ? "선택한 폴더가 존재하지 않습니다."
                : "선택한 파일이 존재하지 않습니다.");
        }

        if (IsSameOrUnder(fullPath, SecureVaultPaths.Root))
        {
            return (false, fullPath, "금고 저장소 또는 임시 작업 영역은 다시 금고에 넣을 수 없습니다.");
        }

        try
        {
            SmartPerformanceDoctor.SecurityLab.Hardening.VaultExportWriter.ValidateAncestors(fullPath);
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                return (false, fullPath, "정션·심볼릭 링크·재분석 지점은 금고 입력으로 사용할 수 없습니다.");
            }
        }
        catch (IOException ex) when (ex.Message == "VAULT-REPARSE-POINT")
        {
            return (false, fullPath, "정션·심볼릭 링크·재분석 지점은 금고 입력으로 사용할 수 없습니다.");
        }
        catch
        {
            return (false, fullPath, "대상 속성을 확인할 권한이 없습니다.");
        }

        return (true, fullPath, string.Empty);
    }

    private static bool IsSameOrUnder(string candidate, string parent)
    {
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd('\\', '/');
        var normalizedParent = Path.GetFullPath(parent).TrimEnd('\\', '/');
        return string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
