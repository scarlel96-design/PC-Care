namespace SmartPerformanceDoctor.AstraVault.Repair;

/// <summary>Repair posture classification only (no automatic repair in E-3).</summary>
public enum Av3RepairClassification
{
    Healthy = 1,
    RedundancyDegraded = 2,
    RepairRecommended = 3,
    RepairRequired = 4,
    CorruptBlocked = 5,
    RollbackSuspected = 6,
    ManualReviewRequired = 7
}