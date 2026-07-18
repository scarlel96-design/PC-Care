namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

public sealed class Av3HeaderCopyDurabilityState
{
    public bool Copy0Durable { get; init; }
    public bool Copy1Durable { get; init; }
    public bool Copy2Durable { get; init; }
    public bool Copy0ConflictsWithCopy1 { get; init; }
    public bool Copy1ConflictsWithCopy2 { get; init; }
    public bool StaleCopyPresent { get; init; }
    public bool UnauthenticatedHighGeneration { get; init; }

    public int DurableCount =>
        (Copy0Durable ? 1 : 0) + (Copy1Durable ? 1 : 0) + (Copy2Durable ? 1 : 0);
}