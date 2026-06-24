using System.Text;

namespace SmartPerformanceDoctor.App.Services.Security;

public static class SecureVaultPathHelper
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static bool LooksLikeEncryptedRelativePath(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored) || !stored.Contains('|'))
        {
            return false;
        }

        var parts = stored.Split('|');
        if (parts.Length != 3)
        {
            return false;
        }

        return parts.All(IsLikelyBase64);
    }

    public static bool ContainsInvalidPathCharacters(string value) =>
        value.IndexOfAny(Path.GetInvalidPathChars()) >= 0
        || value.Contains('|')
        || value.Contains(':')
        || value.Contains('*')
        || value.Contains('?')
        || value.Contains('"')
        || value.Contains('<')
        || value.Contains('>');

    public static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("대상 폴더 경로가 비어 있습니다.");
        }

        return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public static string NormalizeRelative(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            throw new ArgumentException("상대 경로가 비어 있습니다.");
        }

        var normalized = relative.Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("상대 경로가 비어 있습니다.");
        }

        if (LooksLikeEncryptedRelativePath(normalized) || normalized.Contains('|'))
        {
            throw new ArgumentException("암호화된 경로 데이터가 복호화되지 않았습니다.");
        }

        if (normalized.Contains(':') || normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("허용되지 않은 상대 경로입니다.");
        }

        return normalized;
    }

    public static string SanitizeRelativePath(string relative) => NormalizeRelative(relative);

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("파일 이름이 비어 있습니다.");
        }

        var cleaned = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            cleaned.Append(Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch);
        }

        var result = cleaned.ToString().Trim().TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("유효한 파일 이름이 아닙니다.");
        }

        var stem = Path.GetFileNameWithoutExtension(result);
        if (ReservedDeviceNames.Contains(stem))
        {
            result = "_" + result;
        }

        return result;
    }

    public static string CombineUnderRoot(string rootDirectory, string relativePath)
    {
        var normalizedRoot = NormalizeDirectory(rootDirectory);
        var relative = NormalizeRelative(relativePath);
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("상대 경로가 비어 있습니다.");
        }

        var current = normalizedRoot;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, SanitizeFileName(segment));
        }

        var fullPath = Path.GetFullPath(current);
        var rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("허용되지 않은 경로입니다.");
        }

        return fullPath;
    }

    public static string? TryDeriveRelativeFromOriginal(string bundleOriginDirectory, string originalFilePath)
    {
        if (string.IsNullOrWhiteSpace(bundleOriginDirectory) || string.IsNullOrWhiteSpace(originalFilePath))
        {
            return null;
        }

        try
        {
            var root = NormalizeDirectory(bundleOriginDirectory);
            var original = Path.GetFullPath(originalFilePath);
            var relative = Path.GetRelativePath(root, original).Replace('\\', '/');
            return NormalizeRelative(relative);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsLikelyBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length % 4 != 0)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '+' or '/' or '=')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}