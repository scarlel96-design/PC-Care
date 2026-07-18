using System.Text.Json;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

public sealed class Av3KillReport
{
    public Av3KillSupportStatus SupportStatus { get; init; }
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Mismatched { get; init; }
    public IReadOnlyList<Av3KillReportEntry> Entries { get; init; } = [];

    public string ToSafeJson() =>
        JsonSerializer.Serialize(new
        {
            support = SupportStatus.ToString(),
            total = Total,
            passed = Passed,
            mismatched = Mismatched,
            entries = Entries.Select(e => new
            {
                marker = e.Marker.ToString(),
                simulated = e.Simulated.ToString(),
                actual = e.Actual.ToString(),
                match = e.Match,
                compare = e.CompareOutcome
            })
        });

    public static bool ContainsForbiddenLeak(string json, ReadOnlySpan<string> forbidden) =>
        Av3FaultMatrixReport.ContainsForbiddenLeak(json, forbidden);
}