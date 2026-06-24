namespace SmartPerformanceDoctor.App.Models;

public sealed record ErrorBundleItem(
    string Name,
    string Path,
    string Status,
    string Message);
