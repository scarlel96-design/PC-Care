namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 durability probe outcome.</summary>
public sealed class Av3DiskDurabilityProbeResult
{
    public bool Success { get; init; }

    public Av3DiskDurabilityFailureReason FailureReason { get; init; }

    public Av3DiskDurabilityCapability Capability { get; init; } = new();

    public ulong FreeBytes { get; init; }

    public string PublicSummary { get; init; } = string.Empty;
}