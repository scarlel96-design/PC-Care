namespace SmartPerformanceDoctor.App.Models.SystemCare;

public enum CareScanMode
{
    Smart,
    Precision
}

public enum CareModuleKind
{
    Registry,
    Disk,
    Privacy,
    Junk,
    Shortcut,
    Optimization,
    Internet,
    Vulnerability,
    Stability
}

public sealed class CareModuleInfo
{
    public CareModuleKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
}

public sealed class CareScanTaskDefinition
{
    public string Id { get; init; } = "";
    public CareModuleKind Module { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IncludedInSmart { get; init; }
}

public sealed class CareFinding
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string RiskLabel { get; init; } = "확인 필요";
    public string RiskCode { get; init; } = "review";
    public bool CanAutoApply { get; init; }
    public string? TargetPath { get; init; }
    public double Confidence { get; init; } = 0.9;
    public string Evidence { get; init; } = "";
    public string DetectionSource { get; init; } = "system-care";
    public string AutoApplyBlockReason { get; init; } = "";
}

public sealed class CareScanResult
{
    public CareScanMode Mode { get; init; }
    public CareModuleKind Module { get; init; }
    public string ModuleTitle { get; init; } = "";
    public string Summary { get; init; } = "";
    public int HealthScore { get; init; }
    public string HealthGrade { get; init; } = "";
    public bool AuditChainValid { get; init; }
    public IReadOnlyList<string> EnabledTasks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CareFinding> Findings { get; init; } = Array.Empty<CareFinding>();
    public string AuditFolder { get; init; } = "";
}

public sealed class CareApplyResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int AppliedCount { get; init; }
    public int SkippedCount { get; init; }
    public string AuditFolder { get; init; } = "";
}