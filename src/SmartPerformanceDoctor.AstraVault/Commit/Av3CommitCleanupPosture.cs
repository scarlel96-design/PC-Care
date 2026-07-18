namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>E-6.2: separates post-auth data trust from cleanup completion (harness only).</summary>
public enum Av3CommitCleanupPosture
{
    NotApplicable = 0,
    Completed = 1,
    CleanupRequired = 2,
    NewGenerationOpenCleanupRequired = 3,
    RedundancyDegradedCleanupRequired = 4,
    CommittedCleanupRequired = 5
}