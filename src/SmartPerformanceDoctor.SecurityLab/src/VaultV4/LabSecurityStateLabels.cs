namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Design §8 / Phase I: full UI security-state label map (KR + machine id).
/// Product and Lab share one mapping so state machine never drifts.
/// </summary>
public static class LabSecurityStateLabels
{
    public const string NotCreated = "NotCreated · 금고 없음";
    public const string Unknown = "Unknown · 상태 미확인";

    public static string Format(LabSecurityState state) => state switch
    {
        LabSecurityState.Locked => "Locked · 잠김",
        LabSecurityState.Unlocking => "Unlocking · 해제 중",
        LabSecurityState.Unlocked => "Unlocked · 쓰기 가능",
        LabSecurityState.ReadOnlyUnlocked => "ReadOnlyUnlocked · 읽기 전용",
        LabSecurityState.Importing => "Importing · 가져오기 중",
        LabSecurityState.Verifying => "Verifying · 무결성 검사 중",
        LabSecurityState.Committing => "Committing · 커밋 중",
        LabSecurityState.CorruptionDetected => "CorruptionDetected · 손상 감지",
        LabSecurityState.RecoveryAvailable => "RecoveryAvailable · 복구 권고",
        LabSecurityState.AutoLockScheduled => "AutoLockScheduled · 자동 잠금 임박",
        LabSecurityState.SessionExpired => "SessionExpired · 세션 만료",
        _ => state + " · " + Unknown
    };

    /// <summary>Map raw state name from API (enum ToString / persisted).</summary>
    public static string FormatName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Unknown;
        }

        if (string.Equals(name, "NotCreated", StringComparison.OrdinalIgnoreCase))
        {
            return NotCreated;
        }

        if (Enum.TryParse<LabSecurityState>(name, ignoreCase: true, out var s))
        {
            return Format(s);
        }

        return name + " · " + Unknown;
    }

    /// <summary>Every design-facing state including pre-create.</summary>
    public static IReadOnlyList<(string Id, string Label)> AllForUi()
    {
        var list = new List<(string, string)>
        {
            ("NotCreated", NotCreated)
        };
        foreach (LabSecurityState s in Enum.GetValues(typeof(LabSecurityState)))
        {
            list.Add((s.ToString(), Format(s)));
        }

        return list;
    }

    public static bool CoversAllEnumValues()
    {
        foreach (LabSecurityState s in Enum.GetValues(typeof(LabSecurityState)))
        {
            var label = Format(s);
            if (string.IsNullOrWhiteSpace(label) || label.Contains("미확인", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
