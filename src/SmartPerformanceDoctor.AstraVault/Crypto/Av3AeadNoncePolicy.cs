using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Nonce length and production vs fixture policy separation.</summary>
public static class Av3AeadNoncePolicy
{
    public const int ProductionRandomNonceEntropyBytes = 24;

    public static int ExpectedNonceLength(ushort suiteId) =>
        suiteId switch
        {
            Av3AeadAlgorithmId.Aes256Gcm => AstraAead.AesNonceSize,
            Av3AeadAlgorithmId.ChaCha12Transitional => AstraAead.ChaChaNonceSize,
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24 => AstraAead.XChaChaNonceSize,
            _ => throw new CryptographicException("av3_crypto_unknown_suite")
        };

    /// <summary>Harness/fixture deterministic nonces — never used for production RNG policy.</summary>
    public static byte[] FixtureNonce(ushort suiteId, ReadOnlySpan<byte> labelSeed)
    {
        var len = ExpectedNonceLength(suiteId);
        var hash = SHA256.HashData(labelSeed);
        var nonce = new byte[len];
        hash.AsSpan(0, len).CopyTo(nonce);
        return nonce;
    }

    public static void ValidateNonceLength(ushort suiteId, ReadOnlySpan<byte> nonce)
    {
        if (nonce.Length != ExpectedNonceLength(suiteId))
        {
            throw new CryptographicException("av3_crypto_nonce_length");
        }
    }
}