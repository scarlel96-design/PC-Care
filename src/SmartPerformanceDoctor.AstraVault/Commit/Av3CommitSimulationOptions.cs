namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Harness-only fault injection for E-6 commit pipeline (not production).</summary>
public sealed class Av3CommitSimulationOptions
{
    public Av3CommitPipelineStep? FailFlushAtStep { get; set; }

    public bool FailReread { get; set; }

    public bool FailAuthentication { get; set; }

    public bool FailCleanup { get; set; }

    public bool PartialWriteTruncate { get; set; }

    public bool HeaderCopyConflict { get; set; }

    public bool DurableHeaderCopy0 { get; set; } = true;

    public bool DurableHeaderCopy1 { get; set; } = true;

    public bool DurableHeaderCopy2 { get; set; } = true;

    /// <summary>Throws <see cref="OperationCanceledException"/> after completing this pipeline step.</summary>
    public Av3CommitPipelineStep? CancelAfterStep { get; set; }

    /// <summary>Throws cancellation after write, before flush for this flush step.</summary>
    public Av3CommitPipelineStep? CancelBeforeFlushAtStep { get; set; }
}