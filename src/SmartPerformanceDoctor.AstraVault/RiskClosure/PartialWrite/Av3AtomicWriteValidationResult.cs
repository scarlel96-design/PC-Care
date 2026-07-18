using SmartPerformanceDoctor.AstraVault.FaultInjection;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.PartialWrite;

public sealed class Av3AtomicWriteValidationResult
{
    public Av3WriteBoundary Boundary { get; init; }
    public Av3PartialWriteMode Mode { get; init; }
    public Av3RecoveryClassification Classification { get; init; }
    public bool MetadataTrusted { get; init; }
    public bool AllowsNewGenerationOpen { get; init; }
    public string PublicReason { get; init; } = "";
}