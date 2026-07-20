using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// XChaCha20-Poly1305 IETF (24-byte nonce) = HChaCha20 + ChaCha20-Poly1305.
/// Design §6 preferred content AEAD for Phase3.
/// </summary>
public static class LabXChaCha20Poly1305
{
    public const int KeySize = 32;
    public const int NonceSize = 24;
    public const int TagSize = 16;

    private static readonly uint[] Sigma =
    {
        0x61707865, 0x3320646e, 0x79622d32, 0x6b206574
    };

    public static void Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce24,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag,
        ReadOnlySpan<byte> aad)
    {
        Validate(key, nonce24);
        var subkey = HChaCha20(key, nonce24[..16]);
        Span<byte> ietf = stackalloc byte[12];
        ietf.Clear();
        nonce24[16..24].CopyTo(ietf[4..]);
        try
        {
            using var chacha = new ChaCha20Poly1305(subkey);
            chacha.Encrypt(ietf, plaintext, ciphertext, tag, aad);
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
        ReadOnlySpan<byte> aad)
    {
        Validate(key, nonce24);
        var subkey = HChaCha20(key, nonce24[..16]);
        Span<byte> ietf = stackalloc byte[12];
        ietf.Clear();
        nonce24[16..24].CopyTo(ietf[4..]);
        try
        {
            using var chacha = new ChaCha20Poly1305(subkey);
            chacha.Decrypt(ietf, ciphertext, tag, plaintext, aad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    private static void Validate(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce24)
    {
        if (key.Length != KeySize)
        {
            throw new CryptographicException("XChaCha key must be 32 bytes");
        }

        if (nonce24.Length != NonceSize)
        {
            throw new CryptographicException("XChaCha nonce must be 24 bytes");
        }
    }

    /// <summary>HChaCha20 (draft-irtf-cfrg-xchacha).</summary>
    internal static byte[] HChaCha20(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce16)
    {
        Span<uint> state = stackalloc uint[16];
        state[0] = Sigma[0];
        state[1] = Sigma[1];
        state[2] = Sigma[2];
        state[3] = Sigma[3];
        for (var i = 0; i < 8; i++)
        {
            state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }

        for (var i = 0; i < 4; i++)
        {
            state[12 + i] = BinaryPrimitives.ReadUInt32LittleEndian(nonce16.Slice(i * 4, 4));
        }

        for (var i = 0; i < 10; i++)
        {
            QuarterRound(ref state[0], ref state[4], ref state[8], ref state[12]);
            QuarterRound(ref state[1], ref state[5], ref state[9], ref state[13]);
            QuarterRound(ref state[2], ref state[6], ref state[10], ref state[14]);
            QuarterRound(ref state[3], ref state[7], ref state[11], ref state[15]);
            QuarterRound(ref state[0], ref state[5], ref state[10], ref state[15]);
            QuarterRound(ref state[1], ref state[6], ref state[11], ref state[12]);
            QuarterRound(ref state[2], ref state[7], ref state[8], ref state[13]);
            QuarterRound(ref state[3], ref state[4], ref state[9], ref state[14]);
        }

        var output = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0, 4), state[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4, 4), state[1]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(8, 4), state[2]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(12, 4), state[3]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(16, 4), state[12]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(20, 4), state[13]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(24, 4), state[14]);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(28, 4), state[15]);
        return output;
    }

    private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        a += b;
        d = RotateLeft(d ^ a, 16);
        c += d;
        b = RotateLeft(b ^ c, 12);
        a += b;
        d = RotateLeft(d ^ a, 8);
        c += d;
        b = RotateLeft(b ^ c, 7);
    }

    private static uint RotateLeft(uint v, int n) => (v << n) | (v >> (32 - n));
}
