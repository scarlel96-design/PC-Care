namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

public sealed class Av3DiskDurabilityInvariantReport
{
    public bool Passed { get; init; }

    public IReadOnlyList<Av3DiskDurabilityInvariantViolation> Violations { get; init; } = [];
}