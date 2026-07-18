using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

public sealed class Av3DryRunOptions
{
    public required string VaultRoot { get; init; }

    public bool TestHarnessInvocation { get; init; } = true;

    public Av3SyntheticFixtureKind FixtureKind { get; init; } = Av3SyntheticFixtureKind.Standard;

    public Av3CommitSimulationOptions Simulation { get; init; } = new();

    public bool RunCleanup { get; init; } = true;
}