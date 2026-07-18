using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.Experimental;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

/// <summary>Child worker entry (invoked only from kill worker exe).</summary>
public static class Av3KillChildWorkerEntry
{
    public static void Run(string vaultRoot, Av3FaultPoint marker, string planPath)
    {
        var fullRoot = Path.GetFullPath(vaultRoot);
        if (!fullRoot.Contains("av3-e3-kill-", StringComparison.OrdinalIgnoreCase)
            && !fullRoot.Contains("av3-e2-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Child worker vault root blocked.");
        }

        if (planPath.Contains("spd-vault", StringComparison.OrdinalIgnoreCase)
            || planPath.Contains(".svdb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Child worker plan path blocked.");
        }

        var (plan, ctx) = Av3KillPlanFixture.Load(planPath);
        Av3DurableStorageHarness.RunCommitUntilMarker(vaultRoot, marker, plan, ctx);
    }
}