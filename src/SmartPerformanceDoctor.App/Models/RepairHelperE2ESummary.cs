namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairHelperE2ESummary(
    string Status,
    string Confidence,
    string Summary,
    int Passed,
    int Warnings,
    int Failed,
    IReadOnlyList<RepairHelperE2ECheckItem> Checks);
