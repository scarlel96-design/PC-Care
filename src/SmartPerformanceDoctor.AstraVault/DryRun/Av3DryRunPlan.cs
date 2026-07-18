using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Experimental;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

public sealed class Av3DryRunPlan
{
    public Av3SyntheticVaultFixture Fixture { get; init; } = Av3SyntheticVaultFixture.Create();

    public Av3WritePlan WritePlan { get; init; } = new();

    public Av3HarnessCommitContext Crypto { get; init; } = new();

    public Av3CommitHarnessOptions ToHarnessOptions(Av3DryRunOptions options) =>
        new()
        {
            VaultRoot = options.VaultRoot,
            TestHarnessInvocation = options.TestHarnessInvocation,
            Plan = WritePlan,
            Crypto = Crypto,
            HarnessCipherSuiteId = Fixture.HarnessCipherSuiteId,
            Simulation = options.Simulation
        };

    public static Av3DryRunPlan FromFixture(Av3SyntheticVaultFixture fixture) =>
        new()
        {
            Fixture = fixture,
            WritePlan = fixture.BuildWritePlan(),
            Crypto = fixture.BuildDeterministicCryptoContext()
        };
}