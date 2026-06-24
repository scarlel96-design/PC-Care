using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Security;

public sealed class SecureVaultRateLimitStatus
{
    public bool IsLockedOut { get; init; }
    public int FailedAttempts { get; init; }
    public TimeSpan RemainingLockout { get; init; }
    public string Message { get; init; } = "";
}

internal static class SecureVaultRateLimiter
{
    private const int MaxAttemptsBeforeLockout = 5;
    private const int BaseLockoutSeconds = 30;
    private const int MaxLockoutSeconds = 3600;

    private static readonly byte[] StateMagic = "SPDVRL1\0"u8.ToArray();

    public static SecureVaultRateLimitStatus CheckLockout()
    {
        var state = LoadState();
        if (state.LockedUntilUtc is null || state.LockedUntilUtc <= DateTimeOffset.UtcNow)
        {
            return new SecureVaultRateLimitStatus
            {
                IsLockedOut = false,
                FailedAttempts = state.FailedAttempts,
                Message = ""
            };
        }

        var remaining = state.LockedUntilUtc.Value - DateTimeOffset.UtcNow;
        return new SecureVaultRateLimitStatus
        {
            IsLockedOut = true,
            FailedAttempts = state.FailedAttempts,
            RemainingLockout = remaining,
            Message = $"잠금 해제 시도가 너무 많습니다. {Math.Ceiling(remaining.TotalSeconds)}초 후 다시 시도하세요."
        };
    }

    public static void RecordFailedAttempt()
    {
        var state = LoadState();
        state.FailedAttempts++;
        state.LastFailedUtc = DateTimeOffset.UtcNow;

        if (state.FailedAttempts >= MaxAttemptsBeforeLockout)
        {
            var tier = Math.Min(6, state.FailedAttempts - MaxAttemptsBeforeLockout + 1);
            var lockoutSeconds = Math.Min(MaxLockoutSeconds, BaseLockoutSeconds * (int)Math.Pow(2, tier - 1));
            state.LockedUntilUtc = DateTimeOffset.UtcNow.AddSeconds(lockoutSeconds);
        }

        SaveState(state);
    }

    public static void ResetOnSuccess()
    {
        SaveState(new RateLimitState());
    }

    private static RateLimitState LoadState()
    {
        if (!File.Exists(SecureVaultPaths.RateLimitStateFile))
        {
            return new RateLimitState();
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(SecureVaultPaths.RateLimitStateFile);
            var raw = SecureVaultCrypto.UnprotectWithDpapi(protectedBytes);
            using var ms = new MemoryStream(raw);
            var magic = new byte[StateMagic.Length];
            ms.ReadExactly(magic);
            if (!magic.SequenceEqual(StateMagic))
            {
                return new RateLimitState();
            }

            var json = new byte[ms.Length - ms.Position];
            ms.ReadExactly(json);
            return JsonSerializer.Deserialize<RateLimitState>(json) ?? new RateLimitState();
        }
        catch
        {
            return new RateLimitState();
        }
    }

    private static void SaveState(RateLimitState state)
    {
        if (!SecureVaultPaths.Exists())
        {
            return;
        }

        Directory.CreateDirectory(SecureVaultPaths.MetadataDirectory);
        var json = JsonSerializer.SerializeToUtf8Bytes(state);
        using var ms = new MemoryStream();
        ms.Write(StateMagic);
        ms.Write(json);
        var protectedBytes = SecureVaultCrypto.ProtectWithDpapi(ms.ToArray());
        File.WriteAllBytes(SecureVaultPaths.RateLimitStateFile, protectedBytes);
    }

    private sealed class RateLimitState
    {
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LastFailedUtc { get; set; }
        public DateTimeOffset? LockedUntilUtc { get; set; }
    }
}