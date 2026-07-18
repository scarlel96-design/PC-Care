using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Machine-readable FI matrix outcome (no secrets, paths, or plaintext).</summary>
public sealed class Av3FaultMatrixReport
{
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<Av3FaultMatrixReportEntry> Entries { get; init; } = [];

    public string ToSafeJson()
    {
        var payload = new
        {
            total = Total,
            passed = Passed,
            failed = Failed,
            entries = Entries.Select(e => new
            {
                scenario_id = e.ScenarioId,
                kind = e.Kind,
                expected = e.Expected.ToString(),
                actual = e.Actual.ToString(),
                pass = e.Pass,
                durability_mode = e.DurabilityMode?.ToString()
            })
        };
        return JsonSerializer.Serialize(payload);
    }

    public static bool ContainsForbiddenLeak(string reportJson, ReadOnlySpan<string> forbiddenMarkers)
    {
        foreach (var marker in forbiddenMarkers)
        {
            if (reportJson.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class Av3FaultMatrixReportEntry
{
    public string ScenarioId { get; init; } = "";
    public string Kind { get; init; } = "";
    public Av3RecoveryClassification Expected { get; init; }
    public Av3RecoveryClassification Actual { get; init; }
    public bool Pass { get; init; }
    public Av3DurabilitySimulationMode? DurabilityMode { get; init; }
}