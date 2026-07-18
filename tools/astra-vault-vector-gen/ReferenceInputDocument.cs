using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstraVaultVectorGen;

public sealed class ReferenceInputDocument
{
    [JsonPropertyName("vector_schema_version")]
    public int VectorSchemaVersion { get; init; }

    [JsonPropertyName("format_version")]
    public ushort FormatVersion { get; init; }

    [JsonPropertyName("cipher_suite_id")]
    public ushort CipherSuiteId { get; init; }

    [JsonPropertyName("kdf_suite_id")]
    public ushort KdfSuiteId { get; init; }

    [JsonPropertyName("password_test_only")]
    public string PasswordTestOnly { get; init; } = "";

    [JsonPropertyName("container_id")]
    public string ContainerId { get; init; } = "";

    [JsonPropertyName("vault_id")]
    public string VaultId { get; init; } = "";

    [JsonPropertyName("key_slot_id")]
    public ushort KeySlotId { get; init; }

    [JsonPropertyName("header_copy_ids")]
    public int[] HeaderCopyIds { get; init; } = [];

    [JsonPropertyName("generation")]
    public ulong Generation { get; init; }

    [JsonPropertyName("metadata_generation")]
    public ulong MetadataGeneration { get; init; }

    [JsonPropertyName("parent_metadata_generation")]
    public ulong ParentMetadataGeneration { get; init; }

    [JsonPropertyName("activation_target")]
    public uint ActivationTarget { get; init; }

    [JsonPropertyName("vmk_hex")]
    public string VmkHex { get; init; } = "";

    [JsonPropertyName("kdf_salt_hex")]
    public string KdfSaltHex { get; init; } = "";

    [JsonPropertyName("vmk_wrap_nonce_hex")]
    public string VmkWrapNonceHex { get; init; } = "";

    [JsonPropertyName("activation_nonce_hex")]
    public string ActivationNonceHex { get; init; } = "";

    [JsonPropertyName("metadata_root_parent_hash_hex")]
    public string MetadataRootParentHashHex { get; init; } = "";

    [JsonPropertyName("metadata_root_ciphertext_length")]
    public uint MetadataRootCiphertextLength { get; init; }

    [JsonPropertyName("metadata_root_nonce_hex")]
    public string MetadataRootNonceHex { get; init; } = "";

    [JsonPropertyName("metadata_root_logical_id")]
    public ulong MetadataRootLogicalId { get; init; }

    [JsonPropertyName("metadata_graph_root_digest_hex")]
    public string MetadataGraphRootDigestHex { get; init; } = "";

    [JsonPropertyName("metadata_allocation_root_digest_hex")]
    public string MetadataAllocationRootDigestHex { get; init; } = "";

    [JsonPropertyName("metadata_index_root_digest_hex")]
    public string MetadataIndexRootDigestHex { get; init; } = "";

    [JsonPropertyName("metadata_journal_head_commitment_hex")]
    public string MetadataJournalHeadCommitmentHex { get; init; } = "";

    [JsonPropertyName("metadata_recovery_root_digest_hex")]
    public string MetadataRecoveryRootDigestHex { get; init; } = "";

    [JsonPropertyName("argon2id")]
    public Argon2Input Argon2Id { get; init; } = new();

    [JsonPropertyName("expected_public_error_message")]
    public string ExpectedPublicErrorMessage { get; init; } = "";

    [JsonPropertyName("expected_public_error_names")]
    public string[] ExpectedPublicErrorNames { get; init; } = [];

    public static ReferenceInputDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ReferenceInputDocument>(json)
            ?? throw new InvalidOperationException("reference-input.json invalid.");
    }
}

public sealed class Argon2Input
{
    [JsonPropertyName("memory_kib")]
    public int MemoryKiB { get; init; }

    [JsonPropertyName("iterations")]
    public int Iterations { get; init; }

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; init; }

    [JsonPropertyName("profile_id")]
    public ushort ProfileId { get; init; }
}