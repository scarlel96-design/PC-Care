namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Deterministic process-kill simulation for FI (test-only).</summary>
public static class Av3ProcessKillHarness
{
    /// <summary>E-3: Windows child-process kill via <see cref="Kill.Av3ChildProcessKillHarness"/> (test-only).</summary>
    public static bool ActualProcessKillSupported =>
        OperatingSystem.IsWindows() && Kill.Av3ChildProcessKillHarness.IsSupported;

    public static void SimulateKillAtStep(Av3FaultInjector injector, Av3FaultPoint step, Av3WriteTrace trace)
    {
        injector.MaybeFault(step, trace);
    }
}