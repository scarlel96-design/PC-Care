namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Locked AEAD test vector entry (deterministic key labels only).</summary>
public sealed class Av3AeadVector
{
    public required string VectorId { get; init; }

    public required string KeyLabel { get; init; }

    public required ushort SuiteId { get; init; }

    public required byte[] Nonce { get; init; }

    public required byte[] Aad { get; init; }

    public required byte[] Plaintext { get; init; }

    public required byte[] Ciphertext { get; init; }

    public required byte[] Tag { get; init; }

    public string Kind { get; init; } = "activation";
}