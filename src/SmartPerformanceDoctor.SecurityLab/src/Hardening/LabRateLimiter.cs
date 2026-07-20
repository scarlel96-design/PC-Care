using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>Persistent unlock rate limit (file under vault root). No auto-wipe.</summary>
public static class LabRateLimiter
{
    private const int MaxFailures = 5;
    private const int BaseLockSeconds = 30;
    private const int MaxLockSeconds = 3600;

    private sealed class State
    {
        public int Failures { get; set; }
        public long LockedUntilUnix { get; set; }
    }

    public static void EnsureNotLocked(string vaultRoot)
    {
        var state = Load(vaultRoot);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (state.LockedUntilUnix > now)
        {
            var rem = state.LockedUntilUnix - now;
            throw new InvalidOperationException(
                $"잠금 해제 시도가 너무 많습니다. {rem}초 후 다시 시도하세요.");
        }
    }

    public static void RecordFailure(string vaultRoot)
    {
        var state = Load(vaultRoot);
        state.Failures++;
        if (state.Failures >= MaxFailures)
        {
            var tier = Math.Min(6, state.Failures - MaxFailures + 1);
            var secs = Math.Min(MaxLockSeconds, BaseLockSeconds * (1 << (tier - 1)));
            state.LockedUntilUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + secs;
        }

        Save(vaultRoot, state);
    }

    public static void Reset(string vaultRoot) => Save(vaultRoot, new State());

    public sealed class Snapshot
    {
        public int Failures { get; init; }
        public long LockedUntilUnix { get; init; }
        public bool IsLocked { get; init; }
        public long RemainingLockSeconds { get; init; }

        public string ToUiLine()
        {
            if (IsLocked)
            {
                return $"잠금 해제 제한 중 · {RemainingLockSeconds}초 남음 · 실패 {Failures}회";
            }

            if (Failures > 0)
            {
                return $"최근 실패 {Failures}회 (한도 {MaxFailures}회 후 대기)";
            }

            return "잠금 해제 제한 없음";
        }
    }

    public static Snapshot GetSnapshot(string vaultRoot)
    {
        var state = Load(vaultRoot);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var locked = state.LockedUntilUnix > now;
        return new Snapshot
        {
            Failures = state.Failures,
            LockedUntilUnix = state.LockedUntilUnix,
            IsLocked = locked,
            RemainingLockSeconds = locked ? state.LockedUntilUnix - now : 0
        };
    }

    private static string PathOf(string vaultRoot) =>
        System.IO.Path.Combine(vaultRoot, "recovery", "rate_limit.lab.json");

    private static State Load(string vaultRoot)
    {
        var path = PathOf(vaultRoot);
        if (!File.Exists(path))
        {
            return new State();
        }

        try
        {
            return JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? new State();
        }
        catch
        {
            return new State();
        }
    }

    private static void Save(string vaultRoot, State state)
    {
        var path = PathOf(vaultRoot);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }
}
