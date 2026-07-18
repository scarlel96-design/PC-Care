namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>How the harness simulates durability faults (test-only).</summary>
public enum Av3DurabilitySimulationMode
{
    /// <summary>Normal simulated crash at a commit step.</summary>
    SimulatedProcessKill = 1,

    /// <summary>Flush never reaches durable state.</summary>
    SimulatedFlushFailure = 2,

    /// <summary>Post-flush reread returns truncated/corrupt bytes.</summary>
    SimulatedPostFlushRereadFailure = 3,

    /// <summary>Disk full on next write.</summary>
    SimulatedDiskFull = 4,

    /// <summary>External media removed (I/O failure).</summary>
    SimulatedExternalMediaRemoved = 5,

    /// <summary>
    /// Real OS child-process kill — not implemented in E-2 harness.
    /// See <see cref="Av3ProcessKillHarness.ActualProcessKillSupported"/>.
    /// </summary>
    ActualProcessKillPending = 6
}