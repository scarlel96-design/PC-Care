using SmartPerformanceDoctor.AstraVault.Journal;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3CommitJournalRecorder : IAv3JournalRecorder
{
    private readonly bool _harnessRoute;

    public Av3CommitJournalRecorder(bool harnessRoute) => _harnessRoute = harnessRoute;

    public ValueTask<Av3JournalRecordResult> RecordStateAsync(
        Av3JournalRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (!_harnessRoute)
        {
            Av3DefaultWritePolicy.EnforceDisabledWriterGates();
            Av3WriterAccessGate.DenyJournalProductionRoute();
        }

        var bytes = BuildDigestOnlyJournal(request);
        var scan = Av3JournalConfidentialityValidator.ValidateJournalBytes(bytes);
        return ValueTask.FromResult(new Av3JournalRecordResult
        {
            Durable = false,
            ConfidentialityPassed = scan.Passed
        });
    }

    /// <summary>
    /// Harness/test digest slots use <see cref="Av3JournalDeterministicFixtures"/> (reproducibility).
    /// Production writer enable (not authorized): live digests must follow CSPRNG policy in writer enable checklist.
    /// </summary>
    internal byte[] BuildDigestOnlyJournal(Av3JournalRecordRequest request)
    {
        var journal = new Av3JournalDescriptor
        {
            CipherSuiteId = 1,
            ContainerId = Guid.Empty,
            TransactionId = request.TransactionId,
            PreviousGeneration = request.PreviousGeneration,
            TargetGeneration = request.TargetGeneration,
            PreviousMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot0,
            TargetMetadataRootCiphertextDigest = request.TargetMetadataRootCiphertextDigest.ToArray(),
            ObjectWriteSetDigest = Av3JournalDeterministicFixtures.DigestSlot2,
            MetadataWriteDigest = Av3JournalDeterministicFixtures.DigestSlot3,
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };

        return journal.Write();
    }
}