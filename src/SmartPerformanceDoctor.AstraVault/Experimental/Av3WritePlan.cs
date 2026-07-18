namespace SmartPerformanceDoctor.AstraVault.Experimental;

public sealed class Av3ObjectWriteSet
{
    public byte[] ObjectWriteSetDigest { get; init; } = [];
    public int ObjectCount { get; init; }
}

public sealed class Av3MetadataWriteSet
{
    public byte[] MetadataWriteDigest { get; init; } = [];
    public byte[] TargetMetadataRootCiphertextDigest { get; init; } = [];
}

public sealed class Av3ActivationHeaderWritePlan
{
    public ulong HeaderGeneration { get; init; }
    public byte[] MetadataRootPlaintextCommitment { get; init; } = [];
}

public sealed class Av3WritePlan
{
    public Guid ContainerId { get; init; }
    public Guid TransactionId { get; init; }
    public ulong PreviousGeneration { get; init; }
    public ulong TargetGeneration { get; init; }
    public byte[] PreviousMetadataRootDigest { get; init; } = [];
    public Av3ObjectWriteSet Objects { get; init; } = new();
    public Av3MetadataWriteSet Metadata { get; init; } = new();
    public Av3ActivationHeaderWritePlan Activation { get; init; } = new();
}