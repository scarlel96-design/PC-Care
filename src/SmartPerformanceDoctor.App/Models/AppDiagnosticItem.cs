namespace SmartPerformanceDoctor.App.Models;

public sealed record AppDiagnosticItem(
    string Name,
    string Status,
    string Path,
    string Message);
