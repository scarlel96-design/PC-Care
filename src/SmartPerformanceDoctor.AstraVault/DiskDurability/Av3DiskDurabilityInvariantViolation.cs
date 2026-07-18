namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

public sealed class Av3DiskDurabilityInvariantViolation
{
    public Av3DiskDurabilityInvariant Invariant { get; init; }

    public string PublicCode { get; init; } = string.Empty;
}