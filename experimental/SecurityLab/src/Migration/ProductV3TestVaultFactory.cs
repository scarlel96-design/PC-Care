using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPerformanceDoctor.SecurityLab.Migration;

/// <summary>
/// Builds a minimal product-compatible v3 vault for lab tests only.
/// Uses same envelope/manifest/shard layout as PCCare product.
/// </summary>
public static class ProductV3TestVaultFactory
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static void Create(string vaultRoot, string password, IReadOnlyDictionary<string, byte[]> files)
    {
        Directory.CreateDirectory(vaultRoot);
        Directory.CreateDirectory(Path.Combine(vaultRoot, "data"));
        Directory.CreateDirectory(Path.Combine(vaultRoot, "data", "redundant"));
        Directory.CreateDirectory(Path.Combine(vaultRoot, "metadata"));
        Directory.CreateDirectory(Path.Combine(vaultRoot, "audit"));
        Directory.CreateDirectory(Path.Combine(vaultRoot, "recovery"));

        var salt = RandomNumberGenerator.GetBytes(32);
        // LabFast-ish argon for tests
        var iterations = 1;
        var memoryKb = 8 * 1024;
        var parallelism = 1;
        var algorithm = 1; // Argon2id

        var kek = ProductV3Crypto.DeriveKekArgon2id(password, salt, iterations, memoryKb, parallelism);
        var vaultKey = RandomNumberGenerator.GetBytes(32);
        var metadataKey = ProductV3Crypto.DeriveSubKey(kek, salt, "spd-vault-metadata");
        var macKey = ProductV3Crypto.DeriveSubKey(kek, salt, "spd-vault-mac");
        try
        {
            WriteEnvelope(vaultRoot, salt, kek, vaultKey, algorithm, iterations, memoryKb, parallelism);
            File.WriteAllText(
                Path.Combine(vaultRoot, "vault.svdb"),
                JsonSerializer.Serialize(new
                {
                    format = "spd-vault-v3",
                    kdfAlgorithm = "Argon2id",
                    kdfIterations = iterations,
                    kdfMemoryKb = memoryKb,
                    kdfParallelism = parallelism
                }));

            var entries = new List<object>();
            foreach (var (name, content) in files)
            {
                var entryId = Guid.NewGuid().ToString("N");
                var shardName = $"shard_test_{entryId[..8]}.blob";
                var contentHash = ProductV3Crypto.HashSha256Hex(content);
                var blobFormat = ProductV3Crypto.BlobFormatLayered;
                var aad = Encoding.UTF8.GetBytes($"{entryId}|{contentHash}|v{blobFormat}");

                var dek = RandomNumberGenerator.GetBytes(32);
                var padded = ProductV3Crypto.PadWithRandom(content);
                var inner = ProductV3Crypto.Encrypt(dek, padded, aad);
                CryptographicOperations.ZeroMemory(padded);

                var shardMacKey = ProductV3Crypto.DeriveShardKey(vaultKey, entryId, "spd-shard-mac");
                var shardBytes = ProductV3Crypto.WriteLayeredShard(shardMacKey, inner);
                CryptographicOperations.ZeroMemory(shardMacKey);
                File.WriteAllBytes(Path.Combine(vaultRoot, "data", shardName), shardBytes);
                File.WriteAllBytes(Path.Combine(vaultRoot, "data", "redundant", shardName), shardBytes);

                var wrappedDek = ProductV3Crypto.Encrypt(vaultKey, dek);
                var shardDekKey = ProductV3Crypto.DeriveShardKey(vaultKey, entryId, "spd-shard-dek");
                var wrappedShard = ProductV3Crypto.Encrypt(shardDekKey, dek);
                CryptographicOperations.ZeroMemory(shardDekKey);
                var shardMacKey2 = ProductV3Crypto.DeriveShardKey(vaultKey, entryId, "spd-shard-mac");
                var shardMacHex = Convert.ToHexString(
                    ProductV3Crypto.ComputeShardMac(shardMacKey2, inner.Nonce, inner.Tag, inner.Ciphertext))
                    .ToLowerInvariant();
                CryptographicOperations.ZeroMemory(shardMacKey2);
                CryptographicOperations.ZeroMemory(dek);

                var labelBlob = ProductV3Crypto.Encrypt(metadataKey, Encoding.UTF8.GetBytes(name));
                var relBlob = ProductV3Crypto.Encrypt(metadataKey, Encoding.UTF8.GetBytes(name));
                var rel = $"{Convert.ToBase64String(relBlob.Ciphertext)}|{Convert.ToBase64String(relBlob.Nonce)}|{Convert.ToBase64String(relBlob.Tag)}";

                entries.Add(new
                {
                    entryId,
                    entryKind = "file",
                    shardName,
                    encryptedLabel = Convert.ToBase64String(labelBlob.Ciphertext),
                    labelNonce = Convert.ToBase64String(labelBlob.Nonce),
                    labelTag = Convert.ToBase64String(labelBlob.Tag),
                    relativePath = rel,
                    dekWrapped = Convert.ToBase64String(wrappedDek.Ciphertext),
                    dekNonce = Convert.ToBase64String(wrappedDek.Nonce),
                    dekTag = Convert.ToBase64String(wrappedDek.Tag),
                    dekShardWrapped = Convert.ToBase64String(wrappedShard.Ciphertext),
                    dekShardNonce = Convert.ToBase64String(wrappedShard.Nonce),
                    dekShardTag = Convert.ToBase64String(wrappedShard.Tag),
                    contentSha256 = contentHash,
                    originalSize = content.LongLength,
                    addedAt = DateTimeOffset.UtcNow.ToString("o"),
                    isFolderBundle = false,
                    isSealedAtOrigin = false,
                    blobFormat,
                    shardMac = shardMacHex
                });
            }

            var manifest = new Dictionary<string, object?>
            {
                ["format"] = "spd-vault-v3",
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("o"),
                ["entries"] = entries,
                ["manifestMac"] = ""
            };
            var json1 = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOpts);
            using var hmac = new HMACSHA256(macKey);
            var mac = Convert.ToHexString(hmac.ComputeHash(json1)).ToLowerInvariant();
            manifest["manifestMac"] = mac;
            var json2 = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOpts);
            var enc = ProductV3Crypto.Encrypt(metadataKey, json2);
            using var fs = File.Create(Path.Combine(vaultRoot, "vault_manifest.json.enc"));
            fs.Write(enc.Nonce);
            fs.Write(enc.Tag);
            fs.Write(enc.Ciphertext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(vaultKey);
            CryptographicOperations.ZeroMemory(metadataKey);
            CryptographicOperations.ZeroMemory(macKey);
        }
    }

    private static void WriteEnvelope(
        string root,
        byte[] salt,
        byte[] kek,
        byte[] vaultKey,
        int algorithm,
        int iterations,
        int memoryKb,
        int parallelism)
    {
        var encrypted = ProductV3Crypto.Encrypt(kek, vaultKey);
        using var ms = new MemoryStream();
        ms.Write(ProductV3Crypto.EnvelopeMagic);
        ms.Write(BitConverter.GetBytes(3));
        ms.WriteByte((byte)algorithm);
        ms.Write(salt);
        ms.Write(BitConverter.GetBytes(iterations));
        ms.Write(BitConverter.GetBytes(memoryKb));
        ms.Write(BitConverter.GetBytes(parallelism));
        ms.Write(encrypted.Nonce);
        ms.Write(encrypted.Tag);
        ms.Write(encrypted.Ciphertext);
        var protectedBytes = ProductV3Crypto.ProtectDpapi(ms.ToArray());
        File.WriteAllBytes(Path.Combine(root, "key_envelope.bin"), protectedBytes);
    }
}
