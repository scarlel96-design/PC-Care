namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

public sealed class Av3FaultInjectionResult
{
    public bool CommitCompleted { get; init; }
    public Av3FaultPoint? InjectedFault { get; init; }
    public Av3RecoveryClassification Classification { get; init; }
    public Av3WriteTrace Trace { get; init; } = new();
    public ulong TrustedOpenGeneration { get; init; }
    public bool MetadataTrusted { get; init; }
    public bool ActivationAuthenticated { get; init; }
}