namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>E-6 commit pipeline ordering (matches production writer design).</summary>
public enum Av3CommitPipelineStep
{
    Prepare = 1,
    BuildWritePlan = 2,
    WriteObjects = 3,
    FlushObjects = 4,
    WriteMetadataRoot = 5,
    FlushMetadataRoot = 6,
    RecordJournal = 7,
    FlushJournal = 8,
    WriteActivationHeader = 9,
    FlushActivationHeader = 10,
    PostFlushReread = 11,
    PostFlushAuthentication = 12,
    CommitClassification = 13,
    Cleanup = 14
}