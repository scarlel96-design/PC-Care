namespace SmartPerformanceDoctor.App.Models;

public sealed record ReleaseGateItem(
    string Name,
    string Category,
    string Status,
    string Severity,
    string Message,
    string Path);
