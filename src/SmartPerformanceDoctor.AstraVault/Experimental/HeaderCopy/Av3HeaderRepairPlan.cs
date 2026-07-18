using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;

namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

/// <summary>Read-only repair plan (no automatic repair in E-4).</summary>
public sealed class Av3HeaderRepairPlan
{
    public Av3RepairClassification RepairPosture { get; init; }
    public Av3RecoveryClassification RecoveryOutcome { get; init; }
    public byte AuthoritativeCopyIndex { get; init; }
    public int ValidMatchingCopyCount { get; init; }
    public bool StaleCopiesPresent { get; init; }
    public bool AutomaticRepairAuthorized => false;
    public IReadOnlyList<string> RecommendedActions { get; init; } = [];
}