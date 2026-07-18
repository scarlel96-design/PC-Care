namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3WriterInvariantReport
{
    public bool Passed { get; init; }

    public IReadOnlyList<Av3WriterInvariantViolation> Violations { get; init; } = [];

    public string ToPublicSummary() =>
        Passed ? "invariant_ok" : $"invariant_fail_count={Violations.Count}";
}