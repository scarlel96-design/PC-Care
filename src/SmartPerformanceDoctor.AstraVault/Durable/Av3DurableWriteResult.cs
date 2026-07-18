namespace SmartPerformanceDoctor.AstraVault.Durable;

public sealed class Av3DurableWriteResult
{
    public bool FlushSucceeded { get; init; }
    public bool RereadMatched { get; init; }
    public bool AuthenticationSucceeded { get; init; }
    public string RelativePath { get; init; } = "";
}