namespace SmartPerformanceDoctor.App.Models;

public sealed record ModuleNavigationRequest(string ModuleId, bool AutoRun = false);