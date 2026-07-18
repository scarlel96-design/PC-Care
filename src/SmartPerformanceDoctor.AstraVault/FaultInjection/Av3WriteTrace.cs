namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Ordered commit steps executed in a harness run (no secrets).</summary>
public sealed class Av3WriteTrace
{
    private readonly List<string> _steps = [];

    public IReadOnlyList<string> Steps => _steps;

    public Av3FaultPoint? LastCompletedBeforeFault { get; private set; }

    public void Record(string stepName)
    {
        _steps.Add(stepName);
    }

    public void MarkFault(Av3FaultPoint point)
    {
        LastCompletedBeforeFault = point;
    }

    public bool Contains(string stepName) =>
        _steps.Exists(s => string.Equals(s, stepName, StringComparison.Ordinal));
}