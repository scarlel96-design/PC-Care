namespace SmartPerformanceDoctor.AstraVault.Anchor;

public sealed class Av3TrustedAnchorInvariantReport
{
    public bool Passed { get; init; }

    public IReadOnlyList<Av3TrustedAnchorInvariantViolation> Violations { get; init; } = [];
}