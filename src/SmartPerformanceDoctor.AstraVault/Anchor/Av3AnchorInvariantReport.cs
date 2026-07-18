namespace SmartPerformanceDoctor.AstraVault.Anchor;

public sealed class Av3AnchorInvariantReport
{
    public bool Passed { get; init; }

    public IReadOnlyList<Av3AnchorInvariantViolation> Violations { get; init; } = [];

    public string ToPublicSummary() =>
        Passed ? "anchor_invariant_ok" : $"anchor_invariant_fail_count={Violations.Count}";
}