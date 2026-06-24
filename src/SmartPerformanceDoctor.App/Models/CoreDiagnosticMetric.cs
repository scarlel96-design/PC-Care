namespace SmartPerformanceDoctor.App.Models;

public sealed record CoreDiagnosticMetric(
    string Name,
    string Value,
    string Status,
    string Severity,
    string Detail);
