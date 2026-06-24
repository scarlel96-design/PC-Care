using System.Text.Json;
using System.Text.Json.Serialization;


namespace SmartPerformanceDoctor.App.Services.Security;

internal sealed class SecureVaultManifestDocument
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = "spd-vault-v2";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("entries")]
    public List<SecureVaultManifestEntry> Entries { get; set; } = new();

    [JsonPropertyName("manifestMac")]
    public string ManifestMac { get; set; } = "";
}

internal sealed class SecureVaultManifestEntry
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

    [JsonPropertyName("encryptedOriginalPath")]
    public string? EncryptedOriginalPath { get; set; }

    [JsonPropertyName("originalPathNonce")]
    public string? OriginalPathNonce { get; set; }

    [JsonPropertyName("originalPathTag")]
    public string? OriginalPathTag { get; set; }

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

    [JsonPropertyName("addedAt")]
    public string AddedAt { get; set; } = "";

    [JsonPropertyName("isFolderBundle")]
    public bool IsFolderBundle { get; set; }

    [JsonPropertyName("isSealedAtOrigin")]
    public bool IsSealedAtOrigin { get; set; }

    [JsonPropertyName("blobFormat")]
    public int BlobFormat { get; set; } = 1;

    [JsonPropertyName("shardMac")]
    public string? ShardMac { get; set; }
}

internal static class SecureVaultManifestCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static SecureVaultManifestDocument Load(byte[] plaintext) =>
        JsonSerializer.Deserialize<SecureVaultManifestDocument>(plaintext, JsonOptions)
        ?? new SecureVaultManifestDocument();

    public static byte[] Save(SecureVaultManifestDocument manifest) =>
        JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
}