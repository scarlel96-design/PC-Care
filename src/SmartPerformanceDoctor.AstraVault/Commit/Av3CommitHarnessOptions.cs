using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Experimental;

namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3CommitHarnessOptions
{
    public required string VaultRoot { get; init; }

    public required bool TestHarnessInvocation { get; init; }

    public required Av3WritePlan Plan { get; init; }

    public required Av3HarnessCommitContext Crypto { get; init; }

    public ushort HarnessCipherSuiteId { get; init; } = Av3HarnessCommitCrypto.HarnessCipherSuite;

    public Av3CommitSimulationOptions Simulation { get; init; } = new();

    public static Av3CommitHarnessOptions ForBlockedProductionPlaceholder() =>
        new()
        {
            VaultRoot = string.Empty,
            TestHarnessInvocation = false,
            Plan = new Av3WritePlan(),
            Crypto = new Av3HarnessCommitContext()
        };
}