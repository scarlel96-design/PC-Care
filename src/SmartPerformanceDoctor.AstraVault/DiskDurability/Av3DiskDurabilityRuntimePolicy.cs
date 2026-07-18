using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 disk durability runtime gates (harness enabled; production route off).</summary>
public static class Av3DiskDurabilityRuntimePolicy
{
    public const bool HarnessDiskDurabilityEnabled = true;

    public static bool ProductionDiskDurabilityRouteEnabled =>
        Av3PhaseGate.ProductionWriterEnabled && Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed;
}