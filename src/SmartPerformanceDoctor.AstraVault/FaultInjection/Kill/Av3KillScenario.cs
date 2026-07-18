namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

public sealed class Av3KillScenario
{
    public Av3FaultPoint KillMarker { get; init; }
    public ulong PreviousGeneration { get; init; } = 3;
    public ulong TargetGeneration { get; init; } = 4;
}