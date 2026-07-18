namespace SmartPerformanceDoctor.App.Services.Security;

public enum VaultKdfAlgorithm : byte
{
    Pbkdf2Sha512 = 0,
    Argon2id = 1
}

/// <summary>User-selectable Argon2id cost profiles (PCCare vault v4).</summary>
public enum VaultKdfProfile : byte
{
    /// <summary>~0.5–1s unlock target on typical hardware.</summary>
    Balanced = 0,
    /// <summary>Recommended high security (~1–3s).</summary>
    Strong = 1,
    /// <summary>Offline attack resistance (~3–8s). Explicit user choice only.</summary>
    Extreme = 2
}

public sealed record VaultKdfParameters(
    VaultKdfAlgorithm Algorithm,
    int Iterations,
    int MemoryKb = 0,
    int Parallelism = 0)
{
    public const int DefaultArgon2MemoryKb = 131_072;
    public const int DefaultArgon2Iterations = 3;
    public const int DefaultArgon2Parallelism = 4;

    public static VaultKdfParameters DefaultNewVault { get; } = FromProfile(VaultKdfProfile.Strong);

    public static VaultKdfParameters FromProfile(VaultKdfProfile profile) => profile switch
    {
        VaultKdfProfile.Balanced => new(VaultKdfAlgorithm.Argon2id, 2, 65_536, 4),
        VaultKdfProfile.Extreme => new(VaultKdfAlgorithm.Argon2id, 4, 262_144, 4),
        _ => new(VaultKdfAlgorithm.Argon2id, DefaultArgon2Iterations, DefaultArgon2MemoryKb, DefaultArgon2Parallelism)
    };

    public static VaultKdfParameters LegacyPbkdf2(int iterations) =>
        new(VaultKdfAlgorithm.Pbkdf2Sha512, iterations);

    public string DisplayName =>
        Algorithm switch
        {
            VaultKdfAlgorithm.Argon2id =>
                $"Argon2id ({Iterations}t · {MemoryKb / 1024}MB · p{Parallelism})",
            _ => $"PBKDF2-SHA512 ({Iterations:N0}회)"
        };

    public string ProfileHint =>
        MemoryKb switch
        {
            <= 70_000 => "Balanced",
            >= 200_000 => "Extreme",
            _ => "Strong"
        };
}