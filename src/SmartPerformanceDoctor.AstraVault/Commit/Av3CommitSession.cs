using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3CommitSession : IAv3WriteSession
{
    private readonly Av3CommitHarnessOptions _options;

    public Av3CommitSession(Av3CommitHarnessOptions options)
    {
        Av3WriterAccessGate.EnsureHarnessRoute(options.TestHarnessInvocation, options.VaultRoot);
        _options = options;
        SessionId = Guid.NewGuid();
        AuthenticatedGeneration = options.Plan.PreviousGeneration;
        Coordinator = new Av3CommitTransactionCoordinator(options);
        Policy = Av3DefaultWritePolicy.Instance;
    }

    public Guid SessionId { get; }

    public ulong AuthenticatedGeneration { get; }

    public IAv3TransactionCoordinator Coordinator { get; }

    public IAv3WritePolicy Policy { get; }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}