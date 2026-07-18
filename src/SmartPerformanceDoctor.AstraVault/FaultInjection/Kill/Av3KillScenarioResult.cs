namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

public sealed class Av3KillScenarioResult
{
    public Av3KillSupportStatus SupportStatus { get; init; }
    public Av3FaultPoint KillMarker { get; init; }
    public Av3RecoveryClassification Classification { get; init; }
    public bool ChildKilled { get; init; }
    public bool MarkerReached { get; init; }
    public string VaultRootToken { get; init; } = "";
}