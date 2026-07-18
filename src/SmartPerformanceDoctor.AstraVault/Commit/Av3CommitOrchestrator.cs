using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Disabled production implementation of <see cref="IAv3VaultWriter"/> (E-6).</summary>
public sealed class Av3CommitOrchestrator : IAv3VaultWriter
{
    private readonly Av3CommitHarnessOptions _options;

    internal Av3CommitOrchestrator(Av3CommitHarnessOptions options) => _options = options;

    public ValueTask<Av3VaultCommitResult> CommitAsync(
        Av3VaultCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        Av3DefaultWritePolicy.EnforceDisabledWriterGates();
        Av3WriterAccessGate.DenyProductionCreate();
        return default;
    }

    public ValueTask<IAv3WriteSession> OpenWriteSessionAsync(
        Av3WriteSessionOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        Av3DefaultWritePolicy.EnforceDisabledWriterGates();
        Av3WriterAccessGate.DenyProductionCreate();
        return default;
    }

    public async ValueTask<Av3VaultCommitResult> RunHarnessCommitAsync(CancellationToken cancellationToken = default)
    {
        Av3WriterAccessGate.EnsureHarnessRoute(_options.TestHarnessInvocation, _options.VaultRoot);
        var pipeline = await Av3CommitPipelineRunner.RunHarnessAsync(_options, cancellationToken).ConfigureAwait(false);
        return new Av3VaultCommitResult
        {
            Committed = pipeline.Committed,
            TrustedGeneration = pipeline.Committed
                ? pipeline.Snapshot.AttemptedTargetGeneration
                : pipeline.Snapshot.PreviousAuthenticatedGeneration,
            Classification = pipeline.Classification.ToString()
        };
    }
}