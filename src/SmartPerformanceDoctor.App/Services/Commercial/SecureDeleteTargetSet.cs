using SmartPerformanceDoctor.App.Models.Security;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class SecureDeleteTargetSet
{
    private readonly List<SecureDeleteSelection> _items = new();

    public IReadOnlyList<SecureDeleteSelection> Items => _items;

    public SecureDeleteSelectionResult AddFile(string path) => Add(path, SecureDeleteTargetType.File);

    public SecureDeleteSelectionResult AddDirectory(string path) => Add(path, SecureDeleteTargetType.Directory);

    public bool Remove(SecureDeleteSelection selection) => _items.Remove(selection);

    public void Clear() => _items.Clear();

    private SecureDeleteSelectionResult Add(string path, SecureDeleteTargetType expectedType)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.NotFound, "경로가 비어 있습니다.");
        }

        string normalized;
        try
        {
            normalized = Normalize(path, expectedType == SecureDeleteTargetType.Directory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.Inaccessible, "경로를 해석할 수 없습니다.");
        }

        var fileExists = File.Exists(normalized);
        var directoryExists = Directory.Exists(normalized);
        if (!fileExists && !directoryExists)
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.NotFound, "선택한 대상이 존재하지 않습니다.");
        }

        if ((expectedType == SecureDeleteTargetType.File && !fileExists)
            || (expectedType == SecureDeleteTargetType.Directory && !directoryExists))
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.WrongType, "선택한 대상 유형이 일치하지 않습니다.");
        }

        var (allowed, reason) = PathSafetyGuard.Evaluate(normalized);
        if (!allowed)
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.Blocked, reason);
        }

        bool isReparsePoint;
        try
        {
            isReparsePoint = (File.GetAttributes(normalized) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.Inaccessible, "대상 속성을 확인할 수 없습니다.");
        }

        if (isReparsePoint)
        {
            return Rejected(
                path,
                expectedType,
                SecureDeleteValidationStatus.Blocked,
                "정션·심볼릭 링크·재분석 지점은 보안 삭제 대상으로 추가할 수 없습니다.",
                normalized,
                true);
        }

        if (_items.Any(item => PathsEqual(item.NormalizedPath, normalized)))
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.Blocked, "이미 추가된 대상입니다.", normalized);
        }

        if (_items.Any(item =>
                item.TargetType == SecureDeleteTargetType.Directory
                && IsDescendantOf(normalized, item.NormalizedPath)))
        {
            return Rejected(path, expectedType, SecureDeleteValidationStatus.Blocked, "이미 선택한 상위 폴더에 포함된 대상입니다.", normalized);
        }

        var removedChildren = 0;
        if (expectedType == SecureDeleteTargetType.Directory)
        {
            removedChildren = _items.RemoveAll(item => IsDescendantOf(item.NormalizedPath, normalized));
        }

        var selection = new SecureDeleteSelection(
            path,
            normalized,
            expectedType,
            true,
            false,
            SecureDeleteValidationStatus.Valid,
            null);
        _items.Add(selection);
        return new(true, selection, removedChildren > 0
            ? $"대상을 추가하고 포함된 하위 대상 {removedChildren}개를 정리했습니다."
            : "대상을 추가했습니다.", removedChildren);
    }

    private static SecureDeleteSelectionResult Rejected(
        string? original,
        SecureDeleteTargetType type,
        SecureDeleteValidationStatus status,
        string message,
        string? normalized = null,
        bool reparsePoint = false)
    {
        var selection = new SecureDeleteSelection(
            original ?? string.Empty,
            normalized ?? original ?? string.Empty,
            type,
            false,
            reparsePoint,
            status,
            message);
        return new(false, selection, message);
    }

    internal static string Normalize(string path, bool directory)
    {
        var full = Path.GetFullPath(path);
        if (!directory)
        {
            return full;
        }

        var root = Path.GetPathRoot(full);
        return string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
            ? full
            : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    internal static bool IsDescendantOf(string candidate, string parent)
    {
        if (PathsEqual(candidate, parent))
        {
            return false;
        }

        var prefix = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
