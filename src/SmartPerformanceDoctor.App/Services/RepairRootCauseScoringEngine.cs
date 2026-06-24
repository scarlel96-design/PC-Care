using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class RepairRootCauseScoringEngine
{
    public IReadOnlyList<RepairRootCauseScore> Score(IReadOnlyList<RepairHelperE2ECheckItem> checks)
    {
        var areas = checks.GroupBy(x => x.Area);
        var results = new List<RepairRootCauseScore>();

        foreach (var area in areas)
        {
            var signals = area.Select(ToSignal).ToArray();
            var score = signals.Sum(x => x.Weight);
            var severity = score >= 80 ? "critical" : score >= 45 ? "high" : score >= 20 ? "medium" : "low";
            var explanation = BuildExplanation(area.Key, score, severity, signals);

            results.Add(new RepairRootCauseScore(
                area.Key,
                score,
                severity,
                explanation,
                signals));
        }

        return results.OrderByDescending(x => x.Score).ToArray();
    }

    private static RepairRootCauseSignal ToSignal(RepairHelperE2ECheckItem item)
    {
        var weight = item.Status switch
        {
            "pass" => 0,
            "dry-run" => 5,
            "warning" => 20,
            "helper-not-found" => 70,
            "blocked" => 35,
            "failed" => 60,
            _ => 15
        };

        if (item.Severity == "critical")
        {
            weight += 20;
        }
        else if (item.Severity == "high")
        {
            weight += 10;
        }

        return new RepairRootCauseSignal(
            item.Area,
            item.Action,
            weight,
            item.Severity,
            item.Message);
    }

    private static string BuildExplanation(string area, int score, string severity, IReadOnlyList<RepairRootCauseSignal> signals)
    {
        var top = signals.OrderByDescending(x => x.Weight).FirstOrDefault();
        if (top is null)
        {
            return $"{area}: 신호 없음";
        }

        return $"{area}: score={score}, severity={severity}, strongest={top.Signal}, evidence={top.Evidence}";
    }
}
