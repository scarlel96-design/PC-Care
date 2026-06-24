namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairActionDescriptor(
    string Id,
    string Title,
    string Description,
    string Area,
    string Risk,
    bool RequiresTarget,
    string TargetHint,
    string DefaultTarget);
