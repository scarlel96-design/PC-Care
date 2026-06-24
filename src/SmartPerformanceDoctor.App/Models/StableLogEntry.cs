namespace SmartPerformanceDoctor.App.Models;

public sealed record StableLogEntry(
    string Title,
    string Category,
    string CreatedAt,
    string Path,
    string Preview,
    long SizeBytes);
