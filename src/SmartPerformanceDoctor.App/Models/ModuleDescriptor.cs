namespace SmartPerformanceDoctor.App.Models;

public sealed record ModuleDescriptor(
    string Id,
    string Title,
    string Subtitle,
    string Accent,
    string RiskLevel,
    string[] Pipeline);
