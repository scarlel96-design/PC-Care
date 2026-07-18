using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Experimental;

/// <summary>
/// Experimental harness access. Production writer authorized at 50.4.0 product GO.
/// Harness-only invocations still required for experimental types.
/// </summary>
public static class Av3ExperimentalWriterAccess
{
    public static void EnsureHarnessOnly(bool testHarnessInvocation)
    {
        if (!testHarnessInvocation)
        {
            throw new InvalidOperationException("AV3 experimental writer is test-harness only.");
        }
    }

    public static void EnsureNotProductionServicePath()
    {
        // 50.4.0: production services authorized — experimental types still must not be used as UI services.
        // Intentionally empty: production routes use Commit gate, not experimental types.
        _ = Av3PhaseGate.ProductionWriterEnabled;
    }
}
