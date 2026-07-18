namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

public sealed class Av3SimulatedIoException : IOException
{
    public Av3DurabilitySimulationMode Mode { get; }

    public Av3SimulatedIoException(Av3DurabilitySimulationMode mode)
        : base("Simulated AV3 harness I/O fault.")
    {
        Mode = mode;
    }
}