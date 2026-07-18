namespace SmartPerformanceDoctor.AstraVault.Anchor;

public sealed class Av3AnchorInvariantViolation
{
    public Av3AnchorInvariant Invariant { get; init; }

    public string PublicCode { get; init; } = string.Empty;
}