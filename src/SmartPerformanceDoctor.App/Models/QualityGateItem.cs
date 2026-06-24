namespace SmartPerformanceDoctor.App.Models;

public sealed record QualityGateItem(
    string Name,
    string Status,
    string Severity,
    string Message);
