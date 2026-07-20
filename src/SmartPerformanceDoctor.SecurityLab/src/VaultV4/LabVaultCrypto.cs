using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

public enum LabContentSuite
{
    /// <summary>Legacy AES-256-GCM 12-byte nonce (SPDCHK3).</summary>
    Aes256Gcm = 0,
    /// <summary>Design §6 preferred: XChaCha20-Poly1305 24-byte nonce (SPDCHK4).</summary>
    XChaCha20Poly1305 = 1
}

/// <summary>
/// Chunked AEAD + Argon2id. Phase3: dual suite (AES-GCM + XChaCha).
/// </summary>
public static class LabVaultCrypto
{
    public const int KeySize = 32;
    public const int AesNonceSize = 12;
    /// <summary>AES-GCM wrap/meta nonce (legacy name used by key-wrap helpers).</summary>
    public const int NonceSize = AesNonceSize;
    public const int XChaChaNonceSize = 24;
    public const int TagSize = 16;
    public const int ChunkSize = 1024 * 1024;
    /// <summary>Optional pad for small last chunks when concealed-lite is on (design §7 intro).</summary>
    public const int ConcealedPadBlock = 4096;

    private static readonly byte[] MagicAes = "SPDCHK3"u8.ToArray();
    private static readonly byte[] MagicXch = "SPDCHK4"u8.ToArray();

    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(KeySize);
    public static byte[] GenerateSalt(int size = 32) => RandomNumberGenerator.GetBytes(size);

    public static byte[] DeriveArgon2id(string password, byte[] salt, int iterations, int memoryKb, int parallelism)
    {
        var pw = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon = new Argon2id(pw)
            {
                Salt = salt,
                Iterations = Math.Max(1, iterations),
                MemorySize = Math.Max(64 * 1024, memoryKb),
                DegreeOfParallelism = Math.Max(1, parallelism)
            };
            return argon.GetBytes(KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pw);
        }
    }

    public static byte[] EncryptChunked(
        byte[] key,
        byte[] plaintext,
        byte[] aadPrefix,
        LabContentSuite suite = LabContentSuite.XChaCha20Poly1305,
        bool concealedPad = false)
    {
        using var ms = new MemoryStream();
        var magic = suite == LabContentSuite.XChaCha20Poly1305 ? MagicXch : MagicAes;
        ms.Write(magic);
        var count = plaintext.Length == 0 ? 1 : (plaintext.Length + ChunkSize - 1) / ChunkSize;
        ms.Write(BitConverter.GetBytes(count));
        if (plaintext.Length == 0)
        {
            WriteChunk(ms, key, aadPrefix, 0, Array.Empty<byte>(), suite, concealedPad);
            return ms.ToArray();
        }

        for (var i = 0; i < count; i++)
        {
            var offset = i * ChunkSize;
            var len = Math.Min(ChunkSize, plaintext.Length - offset);
            var slice = plaintext.AsSpan(offset, len).ToArray();
            WriteChunk(ms, key, aadPrefix, i, slice, suite, concealedPad && i == count - 1);
            CryptographicOperations.ZeroMemory(slice);
        }

        return ms.ToArray();
    }

    public static byte[] DecryptChunked(byte[] key, byte[] blob, byte[] aadPrefix)
    {
        if (blob.Length < 7 + 4)
        {
            throw new CryptographicException("not a lab chunk blob");
        }

        LabContentSuite suite;
        int nonceSize;
        if (blob.AsSpan(0, 7).SequenceEqual(MagicXch))
        {
            suite = LabContentSuite.XChaCha20Poly1305;
            nonceSize = XChaChaNonceSize;
        }
        else if (blob.AsSpan(0, 7).SequenceEqual(MagicAes))
        {
            suite = LabContentSuite.Aes256Gcm;
            nonceSize = AesNonceSize;
        }
        else
        {
            throw new CryptographicException("not a lab chunk blob");
        }

        var pos = 7;
        var count = BitConverter.ToInt32(blob, pos);
        pos += 4;
        using var outMs = new MemoryStream();
        for (var i = 0; i < count; i++)
        {
            var nonce = blob.AsSpan(pos, nonceSize).ToArray();
            pos += nonceSize;
            var tag = blob.AsSpan(pos, TagSize).ToArray();
            pos += TagSize;
            var clen = BitConverter.ToInt32(blob, pos);
            pos += 4;
            if (clen < 0 || clen > ChunkSize + ConcealedPadBlock)
            {
                throw new CryptographicException("invalid chunk length");
            }

            var cipher = blob.AsSpan(pos, clen).ToArray();
            pos += clen;
            var aad = BuildAad(aadPrefix, i);
            var plain = new byte[clen];
            DecryptOne(key, nonce, cipher, tag, plain, aad, suite);
            // strip optional pad trailer: last 2 bytes little-endian original length if padded
            var payload = StripConcealedPad(plain);
            outMs.Write(payload);
            CryptographicOperations.ZeroMemory(plain);
        }

        return outMs.ToArray();
    }

    public static void EncryptChunkedToFile(
        byte[] key,
        Stream plaintext,
        Stream output,
        byte[] aadPrefix,
        LabContentSuite suite = LabContentSuite.XChaCha20Poly1305)
    {
        var magic = suite == LabContentSuite.XChaCha20Poly1305 ? MagicXch : MagicAes;
        output.Write(magic);
        var countPos = output.Position;
        output.Write(BitConverter.GetBytes(0));
        var count = 0;
        var buffer = new byte[ChunkSize];
        int read;
        if (plaintext.CanSeek && plaintext.Length == 0)
        {
            WriteChunk(output, key, aadPrefix, 0, Array.Empty<byte>(), suite, false);
            count = 1;
        }
        else
        {
            while ((read = plaintext.Read(buffer, 0, buffer.Length)) > 0)
            {
                var slice = buffer.AsSpan(0, read).ToArray();
                WriteChunk(output, key, aadPrefix, count, slice, suite, false);
                CryptographicOperations.ZeroMemory(slice);
                count++;
            }

            if (count == 0)
            {
                WriteChunk(output, key, aadPrefix, 0, Array.Empty<byte>(), suite, false);
                count = 1;
            }
        }

        var end = output.Position;
        output.Position = countPos;
        output.Write(BitConverter.GetBytes(count));
        output.Position = end;
        CryptographicOperations.ZeroMemory(buffer);
    }

    public static void DecryptChunkedFromFile(byte[] key, Stream input, Stream plaintextOut, byte[] aadPrefix)
    {
        var magic = new byte[7];
        if (input.Read(magic, 0, 7) != 7)
        {
            throw new CryptographicException("truncated magic");
        }

        LabContentSuite suite;
        int nonceSize;
        if (magic.AsSpan().SequenceEqual(MagicXch))
        {
            suite = LabContentSuite.XChaCha20Poly1305;
            nonceSize = XChaChaNonceSize;
        }
        else if (magic.AsSpan().SequenceEqual(MagicAes))
        {
            suite = LabContentSuite.Aes256Gcm;
            nonceSize = AesNonceSize;
        }
        else
        {
            throw new CryptographicException("not a lab chunk blob");
        }

        var countBuf = new byte[4];
        if (input.Read(countBuf, 0, 4) != 4)
        {
            throw new CryptographicException("truncated header");
        }

        var count = BitConverter.ToInt32(countBuf);
        for (var i = 0; i < count; i++)
        {
            var nonce = new byte[nonceSize];
            var tag = new byte[TagSize];
            var lenBuf = new byte[4];
            if (input.Read(nonce, 0, nonceSize) != nonceSize
                || input.Read(tag, 0, TagSize) != TagSize
                || input.Read(lenBuf, 0, 4) != 4)
            {
                throw new CryptographicException("truncated chunk header");
            }

            var clen = BitConverter.ToInt32(lenBuf);
            if (clen < 0 || clen > ChunkSize + ConcealedPadBlock)
            {
                throw new CryptographicException("invalid chunk length");
            }

            var cipher = new byte[clen];
            if (input.Read(cipher, 0, clen) != clen)
            {
                throw new CryptographicException("truncated chunk body");
            }

            var plain = new byte[clen];
            var aad = BuildAad(aadPrefix, i);
            DecryptOne(key, nonce, cipher, tag, plain, aad, suite);
            var payload = StripConcealedPad(plain);
            plaintextOut.Write(payload);
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(cipher);
        }
    }

    private static void WriteChunk(
        Stream ms,
        byte[] key,
        byte[] aadPrefix,
        int index,
        byte[] slice,
        LabContentSuite suite,
        bool concealedPad)
    {
        var plain = slice;
        if (concealedPad && slice.Length > 0 && slice.Length < ConcealedPadBlock)
        {
            plain = new byte[ConcealedPadBlock];
            Buffer.BlockCopy(slice, 0, plain, 0, slice.Length);
            // trailer: original length u16
            BinaryPrimitivesWriteUInt16(plain.AsSpan(ConcealedPadBlock - 2), (ushort)slice.Length);
            plain[ConcealedPadBlock - 3] = 0xA5; // pad marker
        }

        var nonceSize = suite == LabContentSuite.XChaCha20Poly1305 ? XChaChaNonceSize : AesNonceSize;
        var nonce = RandomNumberGenerator.GetBytes(nonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        var aad = BuildAad(aadPrefix, index);
        EncryptOne(key, nonce, plain, cipher, tag, aad, suite);
        ms.Write(nonce);
        ms.Write(tag);
        ms.Write(BitConverter.GetBytes(cipher.Length));
        ms.Write(cipher);
        if (!ReferenceEquals(plain, slice))
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static void EncryptOne(
        byte[] key,
        byte[] nonce,
        byte[] plain,
        byte[] cipher,
        byte[] tag,
        byte[] aad,
        LabContentSuite suite)
    {
        if (suite == LabContentSuite.XChaCha20Poly1305)
        {
            LabXChaCha20Poly1305.Encrypt(key, nonce, plain, cipher, tag, aad);
        }
        else
        {
            using var gcm = new AesGcm(key, TagSize);
            gcm.Encrypt(nonce, plain, cipher, tag, aad);
        }
    }

    private static void DecryptOne(
        byte[] key,
        byte[] nonce,
        byte[] cipher,
        byte[] tag,
        byte[] plain,
        byte[] aad,
        LabContentSuite suite)
    {
        if (suite == LabContentSuite.XChaCha20Poly1305)
        {
            LabXChaCha20Poly1305.Decrypt(key, nonce, cipher, tag, plain, aad);
        }
        else
        {
            using var gcm = new AesGcm(key, TagSize);
            gcm.Decrypt(nonce, cipher, tag, plain, aad);
        }
    }

    private static byte[] StripConcealedPad(byte[] plain)
    {
        if (plain.Length == ConcealedPadBlock && plain[^3] == 0xA5)
        {
            var orig = plain[^2] | (plain[^1] << 8);
            if (orig >= 0 && orig <= ConcealedPadBlock - 3)
            {
                return plain.AsSpan(0, orig).ToArray();
            }
        }

        return plain;
    }

    private static void BinaryPrimitivesWriteUInt16(Span<byte> dest, ushort value)
    {
        dest[0] = (byte)value;
        dest[1] = (byte)(value >> 8);
    }

    private static byte[] BuildAad(byte[] prefix, int index)
    {
        var aad = new byte[prefix.Length + 4];
        Buffer.BlockCopy(prefix, 0, aad, 0, prefix.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(index), 0, aad, prefix.Length, 4);
        return aad;
    }
}
