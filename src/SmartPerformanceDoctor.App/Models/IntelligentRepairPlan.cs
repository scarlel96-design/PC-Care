namespace SmartPerformanceDoctor.App.Models;

public sealed record IntelligentRepairPlan(
    string PlanId,
    string Area,
    string RootCauseHypothesis,
    string Risk,
    string PrimaryAction,
    string VerificationMethod,
    bool RequiresElevation,
    bool RequiresTarget,
    string TargetHint,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> RollbackNotes);
