using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.Migration;

/// <summary>Minimal product v3 crypto surface for offline migration (no App reference).</summary>
internal static class ProductV3Crypto
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int ShardMacSize = 32;
    public const int BlobFormatLegacy = 1;
    public const int BlobFormatLayered = 2;
    public const int BlockPadSize = 4096;

    public static readonly byte[] EnvelopeMagic = "SPDVLT1\0"u8.ToArray();
    public static readonly byte[] ShardMagic = "SPDSH2\0"u8.ToArray();

    public sealed record Blob(byte[] Ciphertext, byte[] Nonce, byte[] Tag);

    public static byte[] UnprotectDpapi(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);

    public static byte[] ProtectDpapi(byte[] data) =>
        ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    public static byte[] DeriveKekArgon2id(string password, byte[] salt, int iterations, int memoryKb, int parallelism)
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

    public static byte[] DeriveKekPbkdf2(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Math.Max(1, iterations),
            HashAlgorithmName.SHA512,
            KeySize);

    public static byte[] DeriveSubKey(byte[] kek, byte[] salt, string info) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, kek, KeySize, salt, Encoding.UTF8.GetBytes(info));

    public static byte[] DeriveShardKey(byte[] vaultKey, string entryId, string purpose) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            vaultKey,
            KeySize,
            Encoding.UTF8.GetBytes(entryId),
            Encoding.UTF8.GetBytes(purpose));

    public static Blob Encrypt(byte[] key, byte[] plaintext, byte[]? aad = null)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var gcm = new AesGcm(key, TagSize);
        gcm.Encrypt(nonce, plaintext, cipher, tag, aad);
        return new Blob(cipher, nonce, tag);
    }

    public static byte[] Decrypt(byte[] key, Blob blob, byte[]? aad = null)
    {
        var plain = new byte[blob.Ciphertext.Length];
        using var gcm = new AesGcm(key, TagSize);
        gcm.Decrypt(blob.Nonce, blob.Ciphertext, blob.Tag, plain, aad);
        return plain;
    }

    public static byte[] Unpad(byte[] padded, int originalSize)
    {
        if (originalSize < 0 || originalSize > padded.Length)
        {
            throw new CryptographicException("pad length");
        }

        return padded.AsSpan(0, originalSize).ToArray();
    }

    public static byte[] PadWithRandom(byte[] plaintext)
    {
        var paddedLength = ((plaintext.Length / BlockPadSize) + 1) * BlockPadSize;
        var padded = new byte[paddedLength];
        Buffer.BlockCopy(plaintext, 0, padded, 0, plaintext.Length);
        if (paddedLength > plaintext.Length)
        {
            RandomNumberGenerator.Fill(padded.AsSpan(plaintext.Length));
        }

        return padded;
    }

    public static byte[] ComputeShardMac(byte[] shardMacKey, byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        using var hmac = new HMACSHA256(shardMacKey);
        hmac.TransformBlock(nonce, 0, nonce.Length, null, 0);
        hmac.TransformBlock(tag, 0, tag.Length, null, 0);
        hmac.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return hmac.Hash ?? throw new CryptographicException("mac");
    }

    public static byte[] WriteLayeredShard(byte[] shardMacKey, Blob inner)
    {
        var mac = ComputeShardMac(shardMacKey, inner.Nonce, inner.Tag, inner.Ciphertext);
        using var ms = new MemoryStream();
        ms.Write(ShardMagic);
        ms.WriteByte(BlobFormatLayered);
        ms.WriteByte(0);
        ms.Write(mac);
        ms.Write(inner.Nonce);
        ms.Write(inner.Tag);
        ms.Write(inner.Ciphertext);
        return ms.ToArray();
    }

    public static Blob ReadShardBlob(byte[] bytes, byte[]? shardMacKey)
    {
        if (bytes.Length >= ShardMagic.Length && bytes.AsSpan(0, ShardMagic.Length).SequenceEqual(ShardMagic))
        {
            if (shardMacKey is null)
            {
                throw new CryptographicException("shard mac key required");
            }

            return ReadLayered(bytes, shardMacKey);
        }

        if (bytes.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("blob short");
        }

        return new Blob(
            bytes.AsSpan(NonceSize + TagSize).ToArray(),
            bytes.AsSpan(0, NonceSize).ToArray(),
            bytes.AsSpan(NonceSize, TagSize).ToArray());
    }

    private static Blob ReadLayered(byte[] bytes, byte[] shardMacKey)
    {
        var offset = ShardMagic.Length;
        var version = bytes[offset++];
        _ = bytes[offset++];
        if (version != BlobFormatLayered)
        {
            throw new CryptographicException("shard version");
        }

        var mac = bytes.AsSpan(offset, ShardMacSize).ToArray();
        offset += ShardMacSize;
        var nonce = bytes.AsSpan(offset, NonceSize).ToArray();
        offset += NonceSize;
        var tag = bytes.AsSpan(offset, TagSize).ToArray();
        offset += TagSize;
        var cipher = bytes.AsSpan(offset).ToArray();
        var expected = ComputeShardMac(shardMacKey, nonce, tag, cipher);
        if (!CryptographicOperations.FixedTimeEquals(mac, expected))
        {
            throw new CryptographicException("shard mac fail");
        }

        return new Blob(cipher, nonce, tag);
    }

    public static string HashSha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}
