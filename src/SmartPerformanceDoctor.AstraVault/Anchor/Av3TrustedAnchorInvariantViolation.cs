namespace SmartPerformanceDoctor.AstraVault.Anchor;

public sealed class Av3TrustedAnchorInvariantViolation
{
    public Av3TrustedAnchorInvariant Invariant { get; init; }

    public string PublicCode { get; init; } = string.Empty;
}