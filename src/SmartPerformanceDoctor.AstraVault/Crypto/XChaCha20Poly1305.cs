using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>XChaCha20-Poly1305 IETF AEAD (24-byte nonce, HChaCha subkey + BCL ChaCha20-Poly1305).</summary>
public static class XChaCha20Poly1305
{
    public static void Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce24,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag,
        ReadOnlySpan<byte> associatedData)
    {
        Av3AeadKeyMaterialPolicy.ValidateKey(key);
        Av3AeadNoncePolicy.ValidateNonceLength(Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24, nonce24);
        var subkey = HChaCha20.DeriveSubkey(key, nonce24[..16]);
        Span<byte> ietfNonce = stackalloc byte[AstraAead.ChaChaNonceSize];
        BuildIetfNonce(nonce24, ietfNonce);
        try
        {
            using var chacha = new ChaCha20Poly1305(subkey);
            chacha.Encrypt(ietfNonce, plaintext, ciphertext, tag, associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    public static void Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce24,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        Span<byte> plaintext,
        ReadOnlySpan<byte> associatedData)
    {
        Av3AeadKeyMaterialPolicy.ValidateKey(key);
        Av3AeadNoncePolicy.ValidateNonceLength(Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24, nonce24);
        var subkey = HChaCha20.DeriveSubkey(key, nonce24[..16]);
        Span<byte> ietfNonce = stackalloc byte[AstraAead.ChaChaNonceSize];
        BuildIetfNonce(nonce24, ietfNonce);
        try
        {
            using var chacha = new ChaCha20Poly1305(subkey);
            chacha.Decrypt(ietfNonce, ciphertext, tag, plaintext, associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    /// <summary>IETF ChaCha20-Poly1305 nonce: 32-bit zero counter + 96-bit tail of XChaCha24 nonce.</summary>
    private static void BuildIetfNonce(ReadOnlySpan<byte> nonce24, Span<byte> ietfNonce12)
    {
        ietfNonce12.Clear();
        nonce24[16..24].CopyTo(ietfNonce12[4..]);
    }
}