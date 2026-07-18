namespace SmartPerformanceDoctor.AstraVault.Experimental;

/// <summary>Step-wise post-flush trust chain (harness-only).</summary>
public sealed record Av3HarnessAuthenticationResult
{
    public bool Success { get; init; }
    public bool ActivationAeadAuthenticated { get; init; }
    public bool MetadataCiphertextDigestVerified { get; init; }
    public bool MetadataRootAeadAuthenticated { get; init; }
    public bool MetadataPlaintextCanonicalValidated { get; init; }
    public bool RootPlaintextCommitmentVerified { get; init; }
    public bool GenerationRollbackValidated { get; init; }
}