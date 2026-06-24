using System.Text;

namespace SmartPerformanceDoctor.App.Services.Security;

public enum SecureVaultPasswordStrength
{
    Weak,
    Fair,
    Strong,
    Excellent
}

public sealed class SecureVaultPasswordValidationResult
{
    public bool IsValid { get; init; }
    public SecureVaultPasswordStrength Strength { get; init; }
    public string Message { get; init; } = "";
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
}

public static class SecureVaultPasswordPolicy
{
    public const int MinLengthNewVault = 12;
    public const int MinLengthLegacy = 8;

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123", "12345678", "123456789", "qwerty123",
        "admin123", "letmein", "welcome1", "changeme", "master123"
    };

    public static SecureVaultPasswordValidationResult ValidateForNewVault(string password)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(password))
        {
            return Invalid(issues, "비밀번호를 입력하세요.");
        }

        if (password.Length < MinLengthNewVault)
        {
            issues.Add($"최소 {MinLengthNewVault}자 이상");
        }

        if (!password.Any(char.IsUpper))
        {
            issues.Add("대문자 1자 이상");
        }

        if (!password.Any(char.IsLower))
        {
            issues.Add("소문자 1자 이상");
        }

        if (!password.Any(char.IsDigit))
        {
            issues.Add("숫자 1자 이상");
        }

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            issues.Add("특수문자 1자 이상");
        }

        if (HasRepeatingRun(password, 4))
        {
            issues.Add("동일 문자 4회 이상 반복 금지");
        }

        if (CommonPasswords.Contains(password) || CommonPasswords.Contains(password.ToLowerInvariant()))
        {
            issues.Add("너무 흔한 비밀번호");
        }

        var strength = ComputeStrength(password);
        if (issues.Count > 0)
        {
            return new SecureVaultPasswordValidationResult
            {
                IsValid = false,
                Strength = strength,
                Message = "비밀번호 정책을 충족하지 않습니다: " + string.Join(", ", issues),
                Issues = issues
            };
        }

        return new SecureVaultPasswordValidationResult
        {
            IsValid = true,
            Strength = strength,
            Message = StrengthLabel(strength),
            Issues = issues
        };
    }

    public static bool IsValidForUnlock(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= MinLengthLegacy;

    public static SecureVaultPasswordStrength ComputeStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return SecureVaultPasswordStrength.Weak;
        }

        var score = 0;
        if (password.Length >= 12)
        {
            score += 2;
        }
        else if (password.Length >= 10)
        {
            score++;
        }

        if (password.Any(char.IsUpper))
        {
            score++;
        }

        if (password.Any(char.IsLower))
        {
            score++;
        }

        if (password.Any(char.IsDigit))
        {
            score++;
        }

        if (password.Any(c => !char.IsLetterOrDigit(c)))
        {
            score += 2;
        }

        if (password.Length >= 16)
        {
            score++;
        }

        var uniqueRatio = (double)password.Distinct().Count() / password.Length;
        if (uniqueRatio >= 0.7)
        {
            score++;
        }

        return score switch
        {
            >= 8 => SecureVaultPasswordStrength.Excellent,
            >= 6 => SecureVaultPasswordStrength.Strong,
            >= 4 => SecureVaultPasswordStrength.Fair,
            _ => SecureVaultPasswordStrength.Weak
        };
    }

    public static string FormatRecoveryKey(byte[] key)
    {
        var hex = Convert.ToHexString(key).ToUpperInvariant();
        var groups = new StringBuilder();
        for (var i = 0; i < hex.Length; i += 4)
        {
            if (groups.Length > 0)
            {
                groups.Append('-');
            }

            groups.Append(hex.AsSpan(i, Math.Min(4, hex.Length - i)));
        }

        return groups.ToString();
    }

    public static bool TryParseRecoveryKey(string input, out byte[] key)
    {
        key = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (normalized.Length != 64 || !normalized.All(Uri.IsHexDigit))
        {
            return false;
        }

        try
        {
            key = Convert.FromHexString(normalized);
            return key.Length == 32;
        }
        catch
        {
            return false;
        }
    }

    private static SecureVaultPasswordValidationResult Invalid(List<string> issues, string message) =>
        new()
        {
            IsValid = false,
            Strength = SecureVaultPasswordStrength.Weak,
            Message = message,
            Issues = issues
        };

    private static bool HasRepeatingRun(string password, int runLength)
    {
        if (password.Length < runLength)
        {
            return false;
        }

        for (var i = 0; i <= password.Length - runLength; i++)
        {
            var allSame = true;
            for (var j = 1; j < runLength; j++)
            {
                if (password[i] != password[i + j])
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                return true;
            }
        }

        return false;
    }

    private static string StrengthLabel(SecureVaultPasswordStrength strength) => strength switch
    {
        SecureVaultPasswordStrength.Excellent => "매우 강함 — 상용 금고 수준",
        SecureVaultPasswordStrength.Strong => "강함",
        SecureVaultPasswordStrength.Fair => "보통",
        _ => "약함"
    };
}