namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

public sealed class Av3KillReportEntry
{
    public Av3FaultPoint Marker { get; init; }
    public Av3RecoveryClassification Simulated { get; init; }
    public Av3RecoveryClassification Actual { get; init; }
    public bool Match { get; init; }
    public string CompareOutcome { get; init; } = "";
}