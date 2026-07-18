namespace SmartPerformanceDoctor.AstraVault.Experimental;

public sealed class Av3HarnessMetadataArtifacts
{
    public byte[] Envelope { get; init; } = [];
    public byte[] CiphertextDigest { get; init; } = [];
    public byte[] PlaintextCommitment { get; init; } = [];
    public byte[] ActivationPayloadDigest { get; init; } = [];
}