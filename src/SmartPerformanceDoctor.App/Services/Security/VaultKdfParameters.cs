namespace SmartPerformanceDoctor.App.Services.Security;

public enum VaultKdfAlgorithm : byte
{
    Pbkdf2Sha512 = 0,
    Argon2id = 1
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

    public static VaultKdfParameters DefaultNewVault { get; } = new(
        VaultKdfAlgorithm.Argon2id,
        DefaultArgon2Iterations,
        DefaultArgon2MemoryKb,
        DefaultArgon2Parallelism);

    public static VaultKdfParameters LegacyPbkdf2(int iterations) =>
        new(VaultKdfAlgorithm.Pbkdf2Sha512, iterations);

    public string DisplayName =>
        Algorithm switch
        {
            VaultKdfAlgorithm.Argon2id =>
                $"Argon2id ({Iterations}t · {MemoryKb / 1024}MB · p{Parallelism})",
            _ => $"PBKDF2-SHA512 ({Iterations:N0}회)"
        };
}