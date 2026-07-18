using System.Text.RegularExpressions;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

public enum LabPasswordStrength
{
    Weak,
    Fair,
    Strong,
    Excellent
}

public sealed class LabPasswordValidation
{
    public bool IsValid { get; init; }
    public LabPasswordStrength Strength { get; init; }
    public string Message { get; init; } = "";
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
}

public static class LabPasswordPolicy
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123", "12345678", "qwerty123", "admin123", "changeme", "letmein"
    };

    public static LabPasswordValidation ValidateForCreate(string password)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(password))
        {
            return Invalid("비밀번호를 입력하세요.");
        }

        if (password.Length < 12)
        {
            issues.Add("최소 12자");
        }

        if (!password.Any(char.IsUpper))
        {
            issues.Add("대문자");
        }

        if (!password.Any(char.IsLower))
        {
            issues.Add("소문자");
        }

        if (!password.Any(char.IsDigit))
        {
            issues.Add("숫자");
        }

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            issues.Add("특수문자");
        }

        if (HasRun(password, 4))
        {
            issues.Add("동일 문자 4회 반복 금지");
        }

        if (Common.Contains(password) || Common.Any(c => password.Contains(c, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add("흔한 패턴");
        }

        if (Regex.IsMatch(password, @"^(19|20)\d{2}"))
        {
            issues.Add("연도 시작 패턴 주의");
        }

        if (HasKeyboardWalk(password))
        {
            issues.Add("키보드 연속 패턴");
        }

        if (Regex.IsMatch(password, @"^(.)\1+$"))
        {
            issues.Add("단일 문자 반복");
        }

        // whitespace-only edges
        if (password != password.Trim())
        {
            issues.Add("앞뒤 공백 금지");
        }

        var strength = Score(password);
        if (issues.Count > 0 || strength == LabPasswordStrength.Weak)
        {
            return new LabPasswordValidation
            {
                IsValid = false,
                Strength = strength,
                Message = "비밀번호 정책 미충족: " + string.Join(", ", issues),
                Issues = issues
            };
        }

        return new LabPasswordValidation
        {
            IsValid = true,
            Strength = strength,
            Message = strength.ToString()
        };
    }

    private static bool HasKeyboardWalk(string password)
    {
        const string rows = "01234567890qwertyuiopasdfghjklzxcvbnm";
        var lower = password.ToLowerInvariant();
        for (var i = 0; i < lower.Length - 3; i++)
        {
            var slice = lower.Substring(i, 4);
            if (rows.Contains(slice, StringComparison.Ordinal)
                || rows.Contains(new string(slice.Reverse().ToArray()), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static LabPasswordValidation Invalid(string msg) =>
        new() { IsValid = false, Strength = LabPasswordStrength.Weak, Message = msg };

    private static bool HasRun(string s, int n)
    {
        if (s.Length < n)
        {
            return false;
        }

        for (var i = 0; i <= s.Length - n; i++)
        {
            if (Enumerable.Range(1, n - 1).All(j => s[i] == s[i + j]))
            {
                return true;
            }
        }

        return false;
    }

    private static LabPasswordStrength Score(string password)
    {
        var score = 0;
        if (password.Length >= 12)
        {
            score += 2;
        }

        if (password.Length >= 16)
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

        return score switch
        {
            >= 8 => LabPasswordStrength.Excellent,
            >= 6 => LabPasswordStrength.Strong,
            >= 4 => LabPasswordStrength.Fair,
            _ => LabPasswordStrength.Weak
        };
    }
}
