using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

public enum AstraCipherSuite : ushort
{
    /// <summary>Transitional ChaCha20-Poly1305 (12-byte nonce) — historical id name.</summary>
    XChaCha20Poly1305 = 1,
    Aes256Gcm = 2,
    /// <summary>S-Class TARGET: XChaCha20-Poly1305 IETF (24-byte extended nonce).</summary>
    XChaCha20Poly1305Ietf24 = 3
}

public sealed record AstraCiphertext(byte[] Nonce, byte[] Tag, byte[] Cipher);

public static class AstraAead
{
    public const int AesNonceSize = 12;
    /// <summary>.NET ChaCha20Poly1305 uses 12-byte nonce; XChaCha20 (24) planned Phase B extension.</summary>
    public const int ChaChaNonceSize = 12;
    public const int XChaChaNonceSize = 24;
    public const int TagSize = 16;

    public static AstraCiphertext Encrypt(
        AstraCipherSuite suite,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData)
    {
        return suite switch
        {
            AstraCipherSuite.XChaCha20Poly1305 => EncryptChaCha(key, plaintext, associatedData),
            AstraCipherSuite.Aes256Gcm => EncryptAesGcm(key, plaintext, associatedData),
            AstraCipherSuite.XChaCha20Poly1305Ietf24 => EncryptXChaCha24(key, plaintext, associatedData),
            _ => throw new CryptographicException("av3_crypto_unsupported_suite")
        };
    }

    public static byte[] Decrypt(
        AstraCipherSuite suite,
        ReadOnlySpan<byte> key,
        AstraCiphertext blob,
        ReadOnlySpan<byte> associatedData)
    {
        return suite switch
        {
            AstraCipherSuite.XChaCha20Poly1305 => DecryptChaCha(key, blob, associatedData),
            AstraCipherSuite.Aes256Gcm => DecryptAesGcm(key, blob, associatedData),
            AstraCipherSuite.XChaCha20Poly1305Ietf24 => DecryptXChaCha24(key, blob, associatedData),
            _ => throw new CryptographicException("av3_crypto_unsupported_suite")
        };
    }

    private static AstraCiphertext EncryptChaCha(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(ChaChaNonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var chacha = new ChaCha20Poly1305(key);
        chacha.Encrypt(nonce, plaintext, cipher, tag, aad);
        return new AstraCiphertext(nonce, tag, cipher);
    }

    private static byte[] DecryptChaCha(ReadOnlySpan<byte> key, AstraCiphertext blob, ReadOnlySpan<byte> aad)
    {
        var plain = new byte[blob.Cipher.Length];
        using var chacha = new ChaCha20Poly1305(key);
        chacha.Decrypt(blob.Nonce, blob.Cipher, blob.Tag, plain, aad);
        return plain;
    }

    private static AstraCiphertext EncryptAesGcm(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag, aad);
        return new AstraCiphertext(nonce, tag, cipher);
    }

    private static byte[] DecryptAesGcm(ReadOnlySpan<byte> key, AstraCiphertext blob, ReadOnlySpan<byte> aad)
    {
        var plain = new byte[blob.Cipher.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(blob.Nonce, blob.Cipher, blob.Tag, plain, aad);
        return plain;
    }

    private static AstraCiphertext EncryptXChaCha24(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(XChaChaNonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        XChaCha20Poly1305.Encrypt(key, nonce, plaintext, cipher, tag, aad);
        return new AstraCiphertext(nonce, tag, cipher);
    }

    private static byte[] DecryptXChaCha24(ReadOnlySpan<byte> key, AstraCiphertext blob, ReadOnlySpan<byte> aad)
    {
        if (blob.Nonce.Length != XChaChaNonceSize)
        {
            throw new CryptographicException("av3_crypto_nonce_length");
        }

        var plain = new byte[blob.Cipher.Length];
        XChaCha20Poly1305.Decrypt(key, blob.Nonce, blob.Cipher, blob.Tag, plain, aad);
        return plain;
    }
}