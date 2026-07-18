using SmartPerformanceDoctor.AstraVault.FaultInjection;

namespace SmartPerformanceDoctor.AstraVault.Experimental;

public sealed class Av3WriteTransaction
{
    internal Av3WriteTransaction(
        Av3WritePlan plan,
        Av3TestStorage storage,
        bool testHarnessInvocation,
        Av3HarnessCommitContext harnessContext)
    {
        Plan = plan;
        Storage = storage;
        TestHarnessInvocation = testHarnessInvocation;
        HarnessContext = harnessContext;
    }

    public Av3WritePlan Plan { get; }
    public Av3TestStorage Storage { get; }
    public bool TestHarnessInvocation { get; }
    public Av3HarnessCommitContext HarnessContext { get; }

    public static Av3WriteTransaction CreateForTestHarness(Av3WritePlan plan, Av3TestStorage storage) =>
        CreateForTestHarness(plan, storage, Av3HarnessCommitContext.Generate(plan));

    public static Av3WriteTransaction CreateForTestHarness(
        Av3WritePlan plan,
        Av3TestStorage storage,
        Av3HarnessCommitContext harnessContext) =>
        new(plan, storage, testHarnessInvocation: true, harnessContext);
}