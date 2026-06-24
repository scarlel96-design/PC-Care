namespace SmartPerformanceDoctor.App.Models;

public sealed record SelfHealingItem(
    string Name,
    string Status,
    string Severity,
    string Message,
    string Path);
