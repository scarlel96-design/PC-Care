namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>On-wire cipher suite identifiers (algorithm id is bound in AAD).</summary>
public static class Av3AeadAlgorithmId
{
    /// <summary>ChaCha20-Poly1305 12-byte nonce — BELOW S-CLASS transitional (suite id 1).</summary>
    public const ushort ChaCha12Transitional = (ushort)AstraCipherSuite.XChaCha20Poly1305;

    public const ushort Aes256Gcm = (ushort)AstraCipherSuite.Aes256Gcm;

    /// <summary>XChaCha20-Poly1305 IETF 24-byte extended nonce — S-Class TARGET (suite id 3).</summary>
    public const ushort XChaCha20Poly1305Ietf24 = 3;

    public static bool IsKnown(ushort id) =>
        id is ChaCha12Transitional or Aes256Gcm or XChaCha20Poly1305Ietf24;

    public static bool IsTransitionalChaCha12(ushort id) => id == ChaCha12Transitional;

    public static bool IsXChaCha24Target(ushort id) => id == XChaCha20Poly1305Ietf24;
}