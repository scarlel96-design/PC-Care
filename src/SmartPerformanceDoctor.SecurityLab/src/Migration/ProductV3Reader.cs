using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SmartPerformanceDoctor.SecurityLab.Migration;

/// <summary>
/// Read-only opener for product spd-vault-v3 layouts (DPAPI envelope + layered shards).
/// Does not modify the product vault. No App project reference.
/// </summary>
public sealed class ProductV3Reader : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    private readonly string _root;
    private byte[]? _vaultKey;
    private byte[]? _metadataKey;
    private byte[]? _macKey;
    private ManifestDoc? _manifest;

    public ProductV3Reader(string productVaultRoot)
    {
        _root = Path.GetFullPath(productVaultRoot);
    }

    public bool IsOpen => _vaultKey is not null && _manifest is not null;

    public void Open(string password)
    {
        if (!V3MigrationDryRun.Analyze(_root).LooksLikeProductVault)
        {
            throw new InvalidOperationException("제품 v3 금고 레이아웃이 아닙니다.");
        }

        var (salt, vaultKey, metadataKey, macKey) = ReadKeyEnvelope(password);
        try
        {
            var manifest = LoadManifest(metadataKey, macKey);
            Lock();
            _vaultKey = vaultKey;
            _metadataKey = metadataKey;
            _macKey = macKey;
            _manifest = manifest;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(vaultKey);
            CryptographicOperations.ZeroMemory(metadataKey);
            CryptographicOperations.ZeroMemory(macKey);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    public IReadOnlyList<ProductV3ExportEntry> ExportFileEntries()
    {
        EnsureOpen();
        var list = new List<ProductV3ExportEntry>();
        foreach (var e in _manifest!.Entries)
        {
            if (string.Equals(e.EntryKind, "folderRoot", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(e.ShardName))
            {
                continue;
            }

            var plain = DecryptEntryPayload(e);
            try
            {
                var label = DecryptLabel(e);
                var relative = DecryptRelativePath(e) ?? label;
                list.Add(new ProductV3ExportEntry
                {
                    EntryId = e.EntryId,
                    DisplayName = Path.GetFileName(label.Replace('\\', '/')) is { Length: > 0 } n ? n : e.EntryId,
                    RelativePath = relative.Replace('\\', '/'),
                    Content = plain,
                    ContentSha256 = e.ContentSha256,
                    EntryKind = e.EntryKind,
                    BundleId = e.BundleId
                });
            }
            catch
            {
                CryptographicOperations.ZeroMemory(plain);
                throw;
            }
        }

        return list;
    }

    public void Dispose() => Lock();

    public void Lock()
    {
        if (_vaultKey is not null)
        {
            CryptographicOperations.ZeroMemory(_vaultKey);
        }

        if (_metadataKey is not null)
        {
            CryptographicOperations.ZeroMemory(_metadataKey);
        }

        if (_macKey is not null)
        {
            CryptographicOperations.ZeroMemory(_macKey);
        }

        _vaultKey = null;
        _metadataKey = null;
        _macKey = null;
        _manifest = null;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("v3 금고가 열려 있지 않습니다.");
        }
    }

    private (byte[] salt, byte[] vaultKey, byte[] metadataKey, byte[] macKey) ReadKeyEnvelope(string password)
    {
        var protectedBytes = File.ReadAllBytes(Path.Combine(_root, "key_envelope.bin"));
        var raw = ProductV3Crypto.UnprotectDpapi(protectedBytes);
        using var ms = new MemoryStream(raw);
        var magic = new byte[ProductV3Crypto.EnvelopeMagic.Length];
        ms.ReadExactly(magic);
        if (!magic.SequenceEqual(ProductV3Crypto.EnvelopeMagic))
        {
            throw new CryptographicException("envelope magic");
        }

        var versionBytes = new byte[4];
        ms.ReadExactly(versionBytes);
        var version = BitConverter.ToInt32(versionBytes);

        byte[] salt;
        int algorithm;
        int iterations;
        int memoryKb = 0;
        int parallelism = 0;
        if (version >= 3)
        {
            algorithm = ms.ReadByte();
            salt = new byte[32];
            ms.ReadExactly(salt);
            iterations = ReadInt32(ms);
            memoryKb = ReadInt32(ms);
            parallelism = ReadInt32(ms);
        }
        else
        {
            algorithm = 0; // PBKDF2
            salt = new byte[32];
            ms.ReadExactly(salt);
            iterations = ReadInt32(ms);
            if (iterations <= 0)
            {
                iterations = 310_000;
            }
        }

        var nonce = new byte[ProductV3Crypto.NonceSize];
        ms.ReadExactly(nonce);
        var tag = new byte[ProductV3Crypto.TagSize];
        ms.ReadExactly(tag);
        var cipher = new byte[(int)(ms.Length - ms.Position)];
        ms.ReadExactly(cipher);

        byte[] kek = algorithm == 1
            ? ProductV3Crypto.DeriveKekArgon2id(password, salt, iterations, memoryKb, parallelism)
            : ProductV3Crypto.DeriveKekPbkdf2(password, salt, iterations);
        try
        {
            var vaultKey = ProductV3Crypto.Decrypt(kek, new ProductV3Crypto.Blob(cipher, nonce, tag));
            var metadataKey = ProductV3Crypto.DeriveSubKey(kek, salt, "spd-vault-metadata");
            var macKey = ProductV3Crypto.DeriveSubKey(kek, salt, "spd-vault-mac");
            return (salt, vaultKey, metadataKey, macKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private ManifestDoc LoadManifest(byte[] metadataKey, byte[] macKey)
    {
        var path = Path.Combine(_root, "vault_manifest.json.enc");
        var bytes = File.ReadAllBytes(path);
        var blob = ProductV3Crypto.ReadShardBlob(bytes, shardMacKey: null);
        var json = ProductV3Crypto.Decrypt(metadataKey, blob);
        var manifest = JsonSerializer.Deserialize<ManifestDoc>(json, JsonOpts)
                       ?? throw new CryptographicException("manifest parse");
        if (!VerifyManifestMac(macKey, json, manifest.ManifestMac ?? ""))
        {
            throw new CryptographicException("manifest mac");
        }

        return manifest;
    }

    private static bool VerifyManifestMac(byte[] macKey, byte[] signedJson, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(signedJson);
        var unsigned = Encoding.UTF8.GetBytes(
            Regex.Replace(text, "\"manifestMac\"\\s*:\\s*\"[^\"]*\"", "\"manifestMac\": \"\""));
        using var hmac = new HMACSHA256(macKey);
        var actual = Convert.ToHexString(hmac.ComputeHash(unsigned)).ToLowerInvariant();
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var doc = JsonSerializer.Deserialize<ManifestDoc>(signedJson, JsonOpts);
        if (doc is null)
        {
            return false;
        }

        doc.ManifestMac = "";
        var re = JsonSerializer.SerializeToUtf8Bytes(doc, JsonOpts);
        var actual2 = Convert.ToHexString(hmac.ComputeHash(re)).ToLowerInvariant();
        return string.Equals(expected, actual2, StringComparison.OrdinalIgnoreCase);
    }

    private byte[] DecryptEntryPayload(ManifestEntry entry)
    {
        var shardPath = Path.Combine(_root, "data", entry.ShardName);
        if (!File.Exists(shardPath))
        {
            var red = Path.Combine(_root, "data", "redundant", entry.ShardName);
            if (File.Exists(red))
            {
                shardPath = red;
            }
            else
            {
                throw new FileNotFoundException("shard missing", entry.ShardName);
            }
        }

        var bytes = File.ReadAllBytes(shardPath);
        var layered = entry.BlobFormat >= ProductV3Crypto.BlobFormatLayered
                      || (bytes.Length >= ProductV3Crypto.ShardMagic.Length
                          && bytes.AsSpan(0, ProductV3Crypto.ShardMagic.Length)
                              .SequenceEqual(ProductV3Crypto.ShardMagic));
        var dek = UnwrapDek(entry);
        try
        {
            byte[]? shardMacKey = null;
            if (layered)
            {
                shardMacKey = ProductV3Crypto.DeriveShardKey(_vaultKey!, entry.EntryId, "spd-shard-mac");
            }

            try
            {
                var blob = ProductV3Crypto.ReadShardBlob(bytes, shardMacKey);
                var aad = layered
                    ? Encoding.UTF8.GetBytes($"{entry.EntryId}|{entry.ContentSha256}|v{entry.BlobFormat}")
                    : null;
                var padded = ProductV3Crypto.Decrypt(dek, blob, aad);
                var plain = layered
                    ? ProductV3Crypto.Unpad(padded, (int)entry.OriginalSize)
                    : padded;
                if (layered && !ReferenceEquals(padded, plain))
                {
                    CryptographicOperations.ZeroMemory(padded);
                }

                var hash = ProductV3Crypto.HashSha256Hex(plain);
                if (!hash.Equals(entry.ContentSha256, StringComparison.OrdinalIgnoreCase))
                {
                    CryptographicOperations.ZeroMemory(plain);
                    throw new CryptographicException("content hash mismatch");
                }

                return plain;
            }
            finally
            {
                if (shardMacKey is not null)
                {
                    CryptographicOperations.ZeroMemory(shardMacKey);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    private byte[] UnwrapDek(ManifestEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DekShardWrapped))
        {
            var shardDekKey = ProductV3Crypto.DeriveShardKey(_vaultKey!, entry.EntryId, "spd-shard-dek");
            try
            {
                return ProductV3Crypto.Decrypt(
                    shardDekKey,
                    new ProductV3Crypto.Blob(
                        Convert.FromBase64String(entry.DekShardWrapped!),
                        Convert.FromBase64String(entry.DekShardNonce ?? ""),
                        Convert.FromBase64String(entry.DekShardTag ?? "")));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(shardDekKey);
            }
        }

        return ProductV3Crypto.Decrypt(
            _vaultKey!,
            new ProductV3Crypto.Blob(
                Convert.FromBase64String(entry.DekWrapped),
                Convert.FromBase64String(entry.DekNonce),
                Convert.FromBase64String(entry.DekTag)));
    }

    private string DecryptLabel(ManifestEntry entry)
    {
        var blob = new ProductV3Crypto.Blob(
            Convert.FromBase64String(entry.EncryptedLabel),
            Convert.FromBase64String(entry.LabelNonce),
            Convert.FromBase64String(entry.LabelTag));
        return Encoding.UTF8.GetString(ProductV3Crypto.Decrypt(_metadataKey!, blob));
    }

    private string? DecryptRelativePath(ManifestEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RelativePath))
        {
            return null;
        }

        var stored = entry.RelativePath;
        if (stored.Contains('|'))
        {
            var parts = stored.Split('|');
            if (parts.Length == 3)
            {
                try
                {
                    var blob = new ProductV3Crypto.Blob(
                        Convert.FromBase64String(parts[0]),
                        Convert.FromBase64String(parts[1]),
                        Convert.FromBase64String(parts[2]));
                    return Encoding.UTF8.GetString(ProductV3Crypto.Decrypt(_metadataKey!, blob))
                        .Replace('\\', '/')
                        .TrimStart('/');
                }
                catch
                {
                    return null;
                }
            }
        }

        return stored.Replace('\\', '/').TrimStart('/');
    }

    private static int ReadInt32(Stream s)
    {
        var b = new byte[4];
        s.ReadExactly(b);
        return BitConverter.ToInt32(b);
    }

    private sealed class ManifestDoc
    {
        [JsonPropertyName("format")]
        public string Format { get; set; } = "";

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = "";

        [JsonPropertyName("entries")]
        public List<ManifestEntry> Entries { get; set; } = new();

        [JsonPropertyName("manifestMac")]
        public string? ManifestMac { get; set; }
    }

    private sealed class ManifestEntry
    {
        [JsonPropertyName("entryId")]
        public string EntryId { get; set; } = "";

        [JsonPropertyName("entryKind")]
        public string EntryKind { get; set; } = "file";

        [JsonPropertyName("bundleId")]
        public string? BundleId { get; set; }

        [JsonPropertyName("relativePath")]
        public string? RelativePath { get; set; }

        [JsonPropertyName("shardName")]
        public string ShardName { get; set; } = "";

        [JsonPropertyName("encryptedLabel")]
        public string EncryptedLabel { get; set; } = "";

        [JsonPropertyName("labelNonce")]
        public string LabelNonce { get; set; } = "";

        [JsonPropertyName("labelTag")]
        public string LabelTag { get; set; } = "";

        [JsonPropertyName("dekWrapped")]
        public string DekWrapped { get; set; } = "";

        [JsonPropertyName("dekNonce")]
        public string DekNonce { get; set; } = "";

        [JsonPropertyName("dekTag")]
        public string DekTag { get; set; } = "";

        [JsonPropertyName("dekShardWrapped")]
        public string? DekShardWrapped { get; set; }

        [JsonPropertyName("dekShardNonce")]
        public string? DekShardNonce { get; set; }

        [JsonPropertyName("dekShardTag")]
        public string? DekShardTag { get; set; }

        [JsonPropertyName("contentSha256")]
        public string ContentSha256 { get; set; } = "";

        [JsonPropertyName("originalSize")]
        public long OriginalSize { get; set; }

        [JsonPropertyName("blobFormat")]
        public int BlobFormat { get; set; } = 1;
    }
}

public sealed class ProductV3ExportEntry
{
    public required string EntryId { get; init; }
    public required string DisplayName { get; init; }
    public required string RelativePath { get; init; }
    public required byte[] Content { get; init; }
    public string ContentSha256 { get; init; } = "";
    public string EntryKind { get; init; } = "file";
    public string? BundleId { get; init; }
}
