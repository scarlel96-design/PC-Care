using SmartPerformanceDoctor.AstraVault.Format;

namespace SmartPerformanceDoctor.AstraVault.Validation;

/// <summary>Read-only metadata-root validation outcome (no manifest/graph materialization).</summary>
public sealed class MetadataRootValidationResult
{
    public bool Authenticated { get; init; }
    public MetadataRootDescriptor Descriptor { get; init; } = null!;
    public MetadataRootPlaintextFields RootFields { get; init; } = null!;
    public byte[] CiphertextDigest { get; init; } = [];
    public byte[] RootPlaintextCommitment { get; init; } = [];
}