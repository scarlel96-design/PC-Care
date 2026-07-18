using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

public enum AstraKdfProfile
{
    Standard,
    LowMemoryFallback
}

public sealed record AstraArgon2Parameters(
    int MemoryKiB,
    int Iterations,
    int Parallelism,
    AstraKdfProfile Profile)
{
    public static AstraArgon2Parameters Standard { get; } = new(262_144, 4, 4, AstraKdfProfile.Standard);
    public static AstraArgon2Parameters LowMemory { get; } = new(65_536, 4, 2, AstraKdfProfile.LowMemoryFallback);

    public bool MeetsMinimum =>
        MemoryKiB >= 65_536 && Iterations >= 3 && Parallelism >= 1;
}

public static class AstraKdf
{
    public const int KeySize = 32;

    public static byte[] DeriveKek(string password, ReadOnlySpan<byte> salt, AstraArgon2Parameters parameters)
    {
        if (!parameters.MeetsMinimum)
        {
            throw new CryptographicException("KDF parameters below minimum.");
        }

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt.ToArray(),
            DegreeOfParallelism = parameters.Parallelism,
            MemorySize = parameters.MemoryKiB,
            Iterations = parameters.Iterations
        };
        return argon2.GetBytes(KeySize);
    }

    public static byte[] DeriveDomainKey(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> salt, string domainLabel)
    {
        var saltArr = salt.ToArray();
        var info = Encoding.UTF8.GetBytes(domainLabel);
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey.ToArray(), KeySize, saltArr, info);
    }

    public static void Zero(byte[]? buffer)
    {
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}