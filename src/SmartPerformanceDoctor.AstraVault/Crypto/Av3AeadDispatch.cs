using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Suite dispatch for AEAD operations (algorithm id must match AAD).</summary>
public static class Av3AeadDispatch
{
    public static IAv3AeadCipher Resolve(ushort suiteId) =>
        suiteId switch
        {
            Av3AeadAlgorithmId.ChaCha12Transitional => Av3ChaCha12LegacyAead.Instance,
            Av3AeadAlgorithmId.Aes256Gcm => throw new CryptographicException("av3_crypto_aes_not_in_e12_fixture"),
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24 => Av3XChaCha24Aead.Instance,
            _ => throw new CryptographicException("av3_crypto_unknown_suite")
        };

    public static AstraCipherSuite ToCipherSuite(ushort suiteId) =>
        suiteId switch
        {
            Av3AeadAlgorithmId.ChaCha12Transitional => AstraCipherSuite.XChaCha20Poly1305,
            Av3AeadAlgorithmId.Aes256Gcm => AstraCipherSuite.Aes256Gcm,
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24 => AstraCipherSuite.XChaCha20Poly1305Ietf24,
            _ => throw new CryptographicException("av3_crypto_unknown_suite")
        };
}