using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Key material handling — zeroization hooks; no secret logging.</summary>
public static class Av3AeadKeyMaterialPolicy
{
    public const int AeadKeySize = 32;

    public static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != AeadKeySize)
        {
            throw new CryptographicException("av3_crypto_key_length");
        }
    }

    public static byte[] DeriveFixtureKey(ReadOnlySpan<byte> labelUtf8) => SHA256.HashData(labelUtf8);
}