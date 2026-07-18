namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Trusted anchor prepare/commit request (harness or production-shaped candidate).</summary>
public sealed class Av3TrustedAnchorRequest
{
    public string VaultRoot { get; init; } = string.Empty;

    public bool TestHarnessInvocation { get; init; }

    public Guid VaultId { get; init; }

    public Guid AnchorId { get; init; }

    public Guid UpdateId { get; init; }

    public ulong TargetGeneration { get; init; }

    public string HeaderRootDigestHex { get; init; } = string.Empty;

    public string MetadataCiphertextDigestHex { get; init; } = string.Empty;

    public string ActivationDigestHex { get; init; } = string.Empty;

    public string CurrentWitnessDigestHex { get; init; } = string.Empty;

    public Av3TrustedAnchorProviderKind RequestedProviderKind { get; init; }
}