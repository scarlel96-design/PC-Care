using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Creates disabled writer implementations on harness-only routes (E-6).</summary>
public static class Av3WriterHarnessFactory
{
    public readonly struct Av3WriterCreateResult
    {
        public bool Success { get; init; }

        public string PublicErrorClass { get; init; }

        public IAv3VaultWriter? Writer { get; init; }
    }

    public static Av3WriterCreateResult TryCreateProductionRoute()
    {
        var publicError = !Av3PhaseGate.ProductionWriterEnabled
            ? Av3WriterAccessGate.ErrorProductionDisabled
            : (!Av3PhaseGate.WriterEnableReady
                ? Av3WriterAccessGate.ErrorWriterEnableNoGo
                : Av3WriterAccessGate.ErrorProductionDisabled);

        return new Av3WriterCreateResult
        {
            Success = false,
            PublicErrorClass = publicError,
            Writer = null
        };
    }

    public static Av3CommitOrchestrator CreateHarnessOrchestrator(Av3CommitHarnessOptions options)
    {
        Av3WriterAccessGate.EnsureHarnessRoute(options.TestHarnessInvocation, options.VaultRoot);
        return new Av3CommitOrchestrator(options);
    }

    public static IAv3DurableStore CreateHarnessDurableStore(Av3CommitHarnessOptions options)
    {
        Av3WriterAccessGate.EnsureHarnessRoute(options.TestHarnessInvocation, options.VaultRoot);
        return new Av3CommitDurableStore(options.VaultRoot, options.Simulation);
    }

    public static Av3WriterCreateResult TryOpenProductionSession()
    {
        try
        {
            Av3WriterAccessGate.EnsureProductionRouteFailClosed();
            return new Av3WriterCreateResult { Success = true, PublicErrorClass = "ok", Writer = null };
        }
        catch (Av3WriterRouteBlockedException ex)
        {
            return new Av3WriterCreateResult { Success = false, PublicErrorClass = ex.PublicErrorClass, Writer = null };
        }
    }

    public static Av3WriterCreateResult TryCreateProductionDurableStore(string vaultRoot)
    {
        try
        {
            Av3WriterAccessGate.EnsureProductionRouteFailClosed();
            _ = new Av3CommitDurableStore(vaultRoot, new Av3CommitSimulationOptions());
            return new Av3WriterCreateResult { Success = true, PublicErrorClass = "ok", Writer = null };
        }
        catch (Av3WriterRouteBlockedException ex)
        {
            return new Av3WriterCreateResult { Success = false, PublicErrorClass = ex.PublicErrorClass, Writer = null };
        }
        catch (InvalidOperationException)
        {
            return new Av3WriterCreateResult
            {
                Success = false,
                PublicErrorClass = Av3WriterAccessGate.ErrorIsolatedRootRequired,
                Writer = null
            };
        }
    }
}