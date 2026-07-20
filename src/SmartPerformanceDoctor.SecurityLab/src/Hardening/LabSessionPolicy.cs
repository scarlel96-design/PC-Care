namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>Idle / max session limits for unlocked vault handles (design §8).</summary>
public sealed class LabSessionPolicy
{
    public static LabSessionPolicy Default { get; } = new();

    /// <summary>Lock after this idle period (default 15 min). 0 = disabled. Mutable for product UI sync.</summary>
    public TimeSpan IdleLock { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Absolute max unlock duration (default 4 h). 0 = disabled.</summary>
    public TimeSpan MaxSession { get; set; } = TimeSpan.FromHours(4);

    /// <summary>Warn when remaining idle time is below this (default 2 min).</summary>
    public TimeSpan IdleWarnBefore { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Read-only sessions use shorter idle (default 30 min if IdleLock longer).</summary>
    public TimeSpan ReadOnlyIdleLock { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Max single import size (default 512 MiB).</summary>
    public long MaxImportBytes { get; set; } = 512L * 1024 * 1024;

    /// <summary>Product auto-lock minutes → idle policy (warn = min(2m, idle/4)).</summary>
    public void ApplyProductAutoLockMinutes(int minutes)
    {
        var m = Math.Clamp(minutes, 1, 240);
        IdleLock = TimeSpan.FromMinutes(m);
        // RO idle: slightly longer than write idle, still bounded
        ReadOnlyIdleLock = TimeSpan.FromMinutes(Math.Min(240, m + Math.Max(5, m / 2)));
        var warn = TimeSpan.FromMinutes(Math.Max(1, Math.Min(2, m / 4.0)));
        if (warn >= IdleLock)
        {
            warn = TimeSpan.FromSeconds(Math.Max(30, IdleLock.TotalSeconds / 5));
        }

        IdleWarnBefore = warn;
    }

    /// <summary>Human countdown line for UI (idle + max session).</summary>
    public static string FormatCountdown(TimeSpan? idle, TimeSpan? session, bool idleWarning, bool writeAllowed)
    {
        if (idle is null && session is null)
        {
            return "";
        }

        static string Fmt(TimeSpan t)
        {
            if (t.TotalHours >= 1)
            {
                return $"{(int)t.TotalHours}시간 {t.Minutes:D2}분";
            }

            if (t.TotalMinutes >= 1)
            {
                return $"{(int)t.TotalMinutes}분 {t.Seconds:D2}초";
            }

            return $"{Math.Max(0, (int)t.TotalSeconds)}초";
        }

        var parts = new List<string>();
        if (idle is not null)
        {
            var prefix = idleWarning ? "⚠ 자동 잠금 임박" : "유휴 잠금";
            parts.Add($"{prefix} {Fmt(idle.Value)}");
        }

        if (session is not null)
        {
            parts.Add($"세션 한도 {Fmt(session.Value)}");
        }

        parts.Add(writeAllowed ? "쓰기" : "읽기 전용");
        return string.Join(" · ", parts);
    }

    public bool IsIdleExpired(
        DateTimeOffset unlockedAt,
        DateTimeOffset lastActivity,
        DateTimeOffset now,
        bool writeAllowed = true)
    {
        var idle = EffectiveIdle(writeAllowed);
        if (idle <= TimeSpan.Zero)
        {
            return false;
        }

        return now - lastActivity > idle;
    }

    public bool IsMaxSessionExpired(DateTimeOffset unlockedAt, DateTimeOffset now)
    {
        if (MaxSession <= TimeSpan.Zero)
        {
            return false;
        }

        return now - unlockedAt > MaxSession;
    }

    public bool IsIdleWarning(
        DateTimeOffset lastActivity,
        DateTimeOffset now,
        bool writeAllowed = true)
    {
        var idle = EffectiveIdle(writeAllowed);
        if (idle <= TimeSpan.Zero || IdleWarnBefore <= TimeSpan.Zero)
        {
            return false;
        }

        var remaining = idle - (now - lastActivity);
        return remaining > TimeSpan.Zero && remaining <= IdleWarnBefore;
    }

    public TimeSpan? RemainingIdle(
        DateTimeOffset lastActivity,
        DateTimeOffset now,
        bool writeAllowed = true)
    {
        var idle = EffectiveIdle(writeAllowed);
        if (idle <= TimeSpan.Zero)
        {
            return null;
        }

        var rem = idle - (now - lastActivity);
        return rem < TimeSpan.Zero ? TimeSpan.Zero : rem;
    }

    public TimeSpan? RemainingSession(DateTimeOffset unlockedAt, DateTimeOffset now)
    {
        if (MaxSession <= TimeSpan.Zero)
        {
            return null;
        }

        var rem = MaxSession - (now - unlockedAt);
        return rem < TimeSpan.Zero ? TimeSpan.Zero : rem;
    }

    private TimeSpan EffectiveIdle(bool writeAllowed)
    {
        if (writeAllowed)
        {
            return IdleLock;
        }

        // RO: use the shorter of configured RO idle and write idle when both set
        if (ReadOnlyIdleLock <= TimeSpan.Zero)
        {
            return IdleLock;
        }

        if (IdleLock <= TimeSpan.Zero)
        {
            return ReadOnlyIdleLock;
        }

        return IdleLock < ReadOnlyIdleLock ? IdleLock : ReadOnlyIdleLock;
    }
}
