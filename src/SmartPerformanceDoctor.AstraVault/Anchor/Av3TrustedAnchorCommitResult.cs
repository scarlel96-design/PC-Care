namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Trusted anchor prepare/commit result.</summary>
public sealed class Av3TrustedAnchorCommitResult
{
    public bool Success { get; init; }

    public bool Committed { get; init; }

    public Av3TrustedAnchorFailureReason FailureReason { get; init; }

    public string PublicErrorClass { get; init; } = string.Empty;

    public Av3TrustedAnchorWitness? Witness { get; init; }
}