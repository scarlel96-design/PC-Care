namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Public anchor witness snapshot (no secrets, paths, or filenames).</summary>
public sealed class Av3AnchorSnapshot
{
    public Guid ContainerId { get; init; }

    public ulong Generation { get; init; }

    public ulong MonotonicCounter { get; init; }

    /// <summary>Hex-encoded public witness digest only.</summary>
    public string WitnessDigestHex { get; init; } = string.Empty;

    public Av3AnchorProviderKind ProviderKind { get; init; }
}