namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairRootCauseScore(
    string Area,
    int Score,
    string Severity,
    string Explanation,
    IReadOnlyList<RepairRootCauseSignal> Signals);
