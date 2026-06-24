namespace SmartPerformanceDoctor.App.Models;

public sealed record CoreDiagnosticSnapshot(
    string Status,
    string Health,
    string Summary,
    string EnginePath,
    string LatestReportPath,
    IReadOnlyList<CoreDiagnosticMetric> Metrics);
