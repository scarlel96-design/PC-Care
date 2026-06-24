namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairLogSummary(
    string Title,
    string CreatedAt,
    string Path,
    string Preview);
