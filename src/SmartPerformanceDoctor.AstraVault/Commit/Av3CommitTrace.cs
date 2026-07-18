namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Public commit steps only — no secrets, paths, or filenames (E-6).</summary>
public sealed class Av3CommitTrace
{
    public List<string> Steps { get; } = [];

    public void Add(Av3CommitPipelineStep step) =>
        Steps.Add(step.ToString());

    public string ToPublicSummary() => string.Join(">", Steps);
}