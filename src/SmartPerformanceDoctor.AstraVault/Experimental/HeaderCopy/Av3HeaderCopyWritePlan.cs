namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

public sealed class Av3HeaderCopyWritePlan
{
    public Guid ContainerId { get; init; }
    public ulong TargetGeneration { get; init; }
    public byte[] HeaderCopyBytes { get; init; } = [];
}