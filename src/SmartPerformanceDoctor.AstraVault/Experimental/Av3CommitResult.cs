using SmartPerformanceDoctor.AstraVault.FaultInjection;

namespace SmartPerformanceDoctor.AstraVault.Experimental;

public sealed class Av3CommitResult
{
    public bool Completed { get; init; }
    public Av3RecoveryClassification Classification { get; init; }
    public Av3FaultInjectionResult? FaultResult { get; init; }
}