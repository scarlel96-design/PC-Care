using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class SecureVaultKdfTests
{
    [Fact]
    public void Argon2id_produces_32_byte_key_distinct_from_pbkdf2()
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var password = "test-vault-password-49";

        var argon2 = DeriveArgon2id(password, salt, iterations: 3, memoryKb: 64 * 1024, parallelism: 2);
        var pbkdf2 = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA512, 32);

        Assert.Equal(32, argon2.Length);
        Assert.Equal(32, pbkdf2.Length);
        Assert.False(argon2.AsSpan().SequenceEqual(pbkdf2));
    }

    [Fact]
    public void Argon2id_derivation_is_deterministic_for_same_inputs()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var password = "repeatable";

        var a = DeriveArgon2id(password, salt, 2, 64 * 1024, 2);
        var b = DeriveArgon2id(password, salt, 2, 64 * 1024, 2);

        Assert.True(a.AsSpan().SequenceEqual(b));
    }

    private static byte[] DeriveArgon2id(string password, byte[] salt, int iterations, int memoryKb, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memoryKb,
            Iterations = iterations
        };
        return argon2.GetBytes(32);
    }
}