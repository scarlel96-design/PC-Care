using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3CommitTransactionCoordinator : IAv3TransactionCoordinator
{
    private readonly Av3CommitHarnessOptions _options;
    private Av3TransactionPhase _lastCompleted;

    public Av3CommitTransactionCoordinator(Av3CommitHarnessOptions options)
    {
        Av3WriterAccessGate.EnsureHarnessRoute(options.TestHarnessInvocation, options.VaultRoot);
        _options = options;
    }

    public async ValueTask<Av3TransactionPhaseResult> AdvancePhaseAsync(
        Av3TransactionPhase phase,
        CancellationToken cancellationToken = default)
    {
        if (phase == Av3TransactionPhase.Preparing)
        {
            _lastCompleted = Av3TransactionPhase.Preparing;
            return Ok(phase);
        }

        if (phase == Av3TransactionPhase.PostFlushAuthentication)
        {
            var result = await Av3CommitPipelineRunner.RunHarnessAsync(_options, cancellationToken).ConfigureAwait(false);
            _lastCompleted = phase;
            return new Av3TransactionPhaseResult
            {
                CompletedPhase = phase,
                Success = result.Committed,
                PublicErrorClass = result.Classification.ToString()
            };
        }

        _lastCompleted = phase;
        return Ok(phase);
    }

    public Av3TransactionPhase LastCompleted => _lastCompleted;

    private static Av3TransactionPhaseResult Ok(Av3TransactionPhase phase) =>
        new() { CompletedPhase = phase, Success = true, PublicErrorClass = "ok" };
}