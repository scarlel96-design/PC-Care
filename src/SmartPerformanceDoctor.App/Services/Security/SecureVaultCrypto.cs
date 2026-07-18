using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SmartPerformanceDoctor.App.Services.Security;

internal static class SecureVaultCrypto
{
    public const int KdfIterationsLegacy = 310_000;
    public const int KdfIterations = 600_000;
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int ShardMacSize = 32;
    public const int BlobFormatLegacy = 1;
    public const int BlobFormatLayered = 2;
    /// <summary>v4 chunked AEAD stream (1 MiB chunks, independent nonces).</summary>
    public const int BlobFormatChunked = 3;
    public const int BlockPadSize = 4096;
    public const int ChunkSize = 1024 * 1024;

    private static readonly byte[] ShardMagic = "SPDSH2\0"u8.ToArray();
    private static readonly byte[] ChunkMagic = "SPDCHK3"u8.ToArray();

    public static byte[] GenerateSalt(int size = 32) => RandomNumberGenerator.GetBytes(size);

    public static byte[] GenerateKey(int size = KeySize) => RandomNumberGenerator.GetBytes(size);

    public static byte[] DeriveKek(string password, byte[] salt, VaultKdfParameters? parameters = null) =>
        DeriveKekWithParameters(password, salt, parameters ?? VaultKdfParameters.DefaultNewVault);

    public static byte[] DeriveKekWithIterations(string password, byte[] salt, int iterations) =>
        DeriveKekWithParameters(password, salt, VaultKdfParameters.LegacyPbkdf2(iterations));

    public static byte[] DeriveKekWithParameters(string password, byte[] salt, VaultKdfParameters parameters) =>
        parameters.Algorithm switch
        {
            VaultKdfAlgorithm.Argon2id => DeriveArgon2id(
                password,
                salt,
                parameters.Iterations,
                parameters.MemoryKb,
                parameters.Parallelism),
            _ => Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                parameters.Iterations,
                HashAlgorithmName.SHA512,
                KeySize)
        };

    private static byte[] DeriveArgon2id(string password, byte[] salt, int iterations, int memoryKb, int parallelism)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = Math.Max(1, parallelism),
                MemorySize = Math.Max(64 * 1024, memoryKb),
                Iterations = Math.Max(1, iterations)
            };
            return argon2.GetBytes(KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static byte[] DeriveRecoveryKek(byte[] recoveryKey, byte[] salt) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            recoveryKey,
            KeySize,
            salt,
            "spd-vault-recovery"u8.ToArray());

    public static byte[] DeriveSubKey(byte[] kek, byte[] salt, string info) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, kek, KeySize, salt, Encoding.UTF8.GetBytes(info));

    public static byte[] DeriveShardKey(byte[] vaultKey, string entryId, string purpose) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            vaultKey,
            KeySize,
            Encoding.UTF8.GetBytes(entryId),
            Encoding.UTF8.GetBytes(purpose));

    public static EncryptedBlob Encrypt(byte[] key, byte[] plaintext, byte[]? associatedData = null)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        return new EncryptedBlob(ciphertext, nonce, tag);
    }

    public static byte[] Decrypt(byte[] key, EncryptedBlob blob, byte[]? associatedData = null)
    {
        var plaintext = new byte[blob.Ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(blob.Nonce, blob.Ciphertext, blob.Tag, plaintext, associatedData);
        return plaintext;
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

    public static byte[] Unpad(byte[] padded, int originalSize)
    {
        if (originalSize < 0 || originalSize > padded.Length)
        {
            throw new CryptographicException("패딩 길이가 올바르지 않습니다.");
        }

        return padded.AsSpan(0, originalSize).ToArray();
    }

    public static byte[] ComputeShardMac(byte[] shardMacKey, byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        using var hmac = new HMACSHA256(shardMacKey);
        hmac.TransformBlock(nonce, 0, nonce.Length, null, 0);
        hmac.TransformBlock(tag, 0, tag.Length, null, 0);
        hmac.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return hmac.Hash ?? throw new CryptographicException("샤드 MAC 계산 실패");
    }

    public static byte[] WriteLayeredShardBlob(byte[] shardMacKey, EncryptedBlob inner)
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

    public static EncryptedBlob ReadLayeredShardBlob(byte[] bytes, byte[] shardMacKey)
    {
        if (bytes.Length < ShardMagic.Length + 2 + ShardMacSize + NonceSize + TagSize)
        {
            throw new CryptographicException("암호화 블록이 손상되었습니다.");
        }

        var offset = 0;
        var magic = bytes.AsSpan(offset, ShardMagic.Length);
        offset += ShardMagic.Length;
        if (!magic.SequenceEqual(ShardMagic))
        {
            return ReadLegacyBlob(bytes);
        }

        var version = bytes[offset++];
        _ = bytes[offset++];
        if (version != BlobFormatLayered)
        {
            throw new CryptographicException("지원하지 않는 샤드 형식입니다.");
        }

        var mac = bytes.AsSpan(offset, ShardMacSize).ToArray();
        offset += ShardMacSize;
        var nonce = bytes.AsSpan(offset, NonceSize).ToArray();
        offset += NonceSize;
        var tag = bytes.AsSpan(offset, TagSize).ToArray();
        offset += TagSize;
        var cipher = bytes.AsSpan(offset).ToArray();
        var expectedMac = ComputeShardMac(shardMacKey, nonce, tag, cipher);
        if (!CryptographicOperations.FixedTimeEquals(mac, expectedMac))
        {
            throw new CryptographicException("샤드 무결성 검증 실패");
        }

        return new EncryptedBlob(cipher, nonce, tag);
    }

    public static EncryptedBlob ReadShardBlob(byte[] bytes, byte[]? shardMacKey)
    {
        if (bytes.Length >= ShardMagic.Length && bytes.AsSpan(0, ShardMagic.Length).SequenceEqual(ShardMagic))
        {
            if (shardMacKey is null)
            {
                throw new CryptographicException("샤드 MAC 키가 필요합니다.");
            }

            return ReadLayeredShardBlob(bytes, shardMacKey);
        }

        return ReadLegacyBlob(bytes);
    }

    private static EncryptedBlob ReadLegacyBlob(byte[] bytes)
    {
        if (bytes.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("암호화 블록이 손상되었습니다.");
        }

        var nonce = bytes.AsSpan(0, NonceSize).ToArray();
        var tag = bytes.AsSpan(NonceSize, TagSize).ToArray();
        var cipher = bytes.AsSpan(NonceSize + TagSize).ToArray();
        return new EncryptedBlob(cipher, nonce, tag);
    }

    public static byte[] ProtectWithDpapi(byte[] data) =>
        ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    public static byte[] UnprotectWithDpapi(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);

    public static string HashSha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static void Zero(byte[]? buffer)
    {
        if (buffer is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(buffer);
    }

    /// <summary>
    /// Chunked AEAD for large payloads — each chunk has independent CSPRNG nonce.
    /// Layout: magic | u32 chunkCount | repeating (nonce12 | tag16 | u32 len | cipher).
    /// </summary>
    public static byte[] EncryptChunked(byte[] key, byte[] plaintext, byte[] aadPrefix)
    {
        using var ms = new MemoryStream();
        ms.Write(ChunkMagic);
        var chunkCount = plaintext.Length == 0 ? 1 : (plaintext.Length + ChunkSize - 1) / ChunkSize;
        ms.Write(BitConverter.GetBytes(chunkCount));

        if (plaintext.Length == 0)
        {
            WriteChunk(ms, key, aadPrefix, 0, Array.Empty<byte>());
            return ms.ToArray();
        }

        for (var i = 0; i < chunkCount; i++)
        {
            var offset = i * ChunkSize;
            var len = Math.Min(ChunkSize, plaintext.Length - offset);
            var slice = plaintext.AsSpan(offset, len).ToArray();
            WriteChunk(ms, key, aadPrefix, i, slice);
            Zero(slice);
        }

        return ms.ToArray();
    }

    public static byte[] DecryptChunked(byte[] key, byte[] blob, byte[] aadPrefix)
    {
        if (blob.Length < ChunkMagic.Length + 4 || !blob.AsSpan(0, ChunkMagic.Length).SequenceEqual(ChunkMagic))
        {
            throw new CryptographicException("청크 암호화 형식이 아닙니다.");
        }

        var offset = ChunkMagic.Length;
        var chunkCount = BitConverter.ToInt32(blob, offset);
        offset += 4;
        if (chunkCount < 0 || chunkCount > 1_000_000)
        {
            throw new CryptographicException("청크 개수가 올바르지 않습니다.");
        }

        using var outMs = new MemoryStream();
        for (var i = 0; i < chunkCount; i++)
        {
            if (offset + NonceSize + TagSize + 4 > blob.Length)
            {
                throw new CryptographicException("청크 헤더가 손상되었습니다.");
            }

            var nonce = blob.AsSpan(offset, NonceSize).ToArray();
            offset += NonceSize;
            var tag = blob.AsSpan(offset, TagSize).ToArray();
            offset += TagSize;
            var clen = BitConverter.ToInt32(blob, offset);
            offset += 4;
            if (clen < 0 || offset + clen > blob.Length)
            {
                throw new CryptographicException("청크 길이가 올바르지 않습니다.");
            }

            var cipher = blob.AsSpan(offset, clen).ToArray();
            offset += clen;
            var aad = BuildChunkAad(aadPrefix, i);
            var plain = Decrypt(key, new EncryptedBlob(cipher, nonce, tag), aad);
            outMs.Write(plain);
            Zero(plain);
            Zero(cipher);
            Zero(nonce);
            Zero(tag);
        }

        return outMs.ToArray();
    }

    public static bool IsChunkedBlob(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= ChunkMagic.Length && bytes[..ChunkMagic.Length].SequenceEqual(ChunkMagic);

    private static void WriteChunk(MemoryStream ms, byte[] key, byte[] aadPrefix, int index, byte[] slice)
    {
        var aad = BuildChunkAad(aadPrefix, index);
        var box = Encrypt(key, slice, aad);
        ms.Write(box.Nonce);
        ms.Write(box.Tag);
        ms.Write(BitConverter.GetBytes(box.Ciphertext.Length));
        ms.Write(box.Ciphertext);
    }

    private static byte[] BuildChunkAad(byte[] aadPrefix, int index)
    {
        var aad = new byte[aadPrefix.Length + 4];
        Buffer.BlockCopy(aadPrefix, 0, aad, 0, aadPrefix.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(index), 0, aad, aadPrefix.Length, 4);
        return aad;
    }
}

internal sealed record EncryptedBlob(byte[] Ciphertext, byte[] Nonce, byte[] Tag);