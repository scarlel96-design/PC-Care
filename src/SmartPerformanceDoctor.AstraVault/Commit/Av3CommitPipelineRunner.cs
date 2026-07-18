using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>E-6 harness commit pipeline (14 steps, fail-closed).</summary>
public static class Av3CommitPipelineRunner
{
    public sealed class Av3CommitPipelineResult
    {
        public Av3RecoveryClassification Classification { get; init; }

        public Av3RepairClassification Repair { get; init; }

        public bool Committed { get; init; }

        public bool PostAuthDataTrusted { get; init; }

        public Av3CommitCleanupPosture CleanupPosture { get; init; }

        public Av3CommitTrace Trace { get; init; } = new();

        public Av3CommitSnapshot Snapshot { get; init; } = new();

        public bool Cancelled { get; init; }

        public Av3WriterCancellationReport? Cancellation { get; init; }
    }

    public static async Task<Av3CommitPipelineResult> RunHarnessAsync(
        Av3CommitHarnessOptions options,
        CancellationToken cancellationToken = default)
    {
        Av3WriterAccessGate.EnsureHarnessRoute(options.TestHarnessInvocation, options.VaultRoot);
        Av3DefaultWritePolicy.EnforceDisabledWriterGates();
        try
        {
            using var guard = Av3WriterCommitGuard.EnterHarnessCommit(options.VaultRoot, options.Plan.TransactionId);
            var trace = new Av3CommitTrace();
            var snapshot = new Av3CommitSnapshot
            {
                PreviousAuthenticatedGeneration = options.Plan.PreviousGeneration,
                AttemptedTargetGeneration = options.Plan.TargetGeneration
            };

            var store = new Av3CommitDurableStore(options.VaultRoot, options.Simulation);
            var journalRecorder = new Av3CommitJournalRecorder(harnessRoute: true);
            var headerCommitter = new Av3CommitHeaderCommitter(options.VaultRoot, options.Simulation);
            var recovery = new Av3CommitRecoveryManager();

            try
            {
                trace.Add(Av3CommitPipelineStep.Prepare);
                trace.Add(Av3CommitPipelineStep.BuildWritePlan);
                ThrowIfCancelled(options, Av3CommitPipelineStep.BuildWritePlan, cancellationToken);
                Directory.CreateDirectory(options.VaultRoot);

                trace.Add(Av3CommitPipelineStep.WriteObjects);
                ThrowIfCancelled(options, Av3CommitPipelineStep.WriteObjects, cancellationToken);
                var objectCipher = Av3HarnessCommitCrypto.EncryptHarnessObject(
                    options.Crypto.Vmk,
                    options.Plan.TargetGeneration,
                    options.Crypto.HarnessObjectPlaintext);
                await WriteDurableAsync(store, Av3DurableFileLayout.ObjectsRelative, objectCipher, options, Av3CommitPipelineStep.FlushObjects, cancellationToken)
                    .ConfigureAwait(false);
                snapshot.ObjectsFlushed = true;
                ThrowIfCancelled(options, Av3CommitPipelineStep.FlushObjects, cancellationToken);

                trace.Add(Av3CommitPipelineStep.WriteMetadataRoot);
                ThrowIfCancelled(options, Av3CommitPipelineStep.WriteMetadataRoot, cancellationToken);
                var meta = Av3HarnessCommitCrypto.BuildMetadataRootArtifacts(
                    options.Crypto.Vmk,
                    options.Plan,
                    options.HarnessCipherSuiteId);
                await WriteDurableAsync(store, Av3DurableFileLayout.MetadataRelative, meta.Envelope, options, Av3CommitPipelineStep.FlushMetadataRoot, cancellationToken)
                    .ConfigureAwait(false);
                snapshot.MetadataFlushed = true;
                ThrowIfCancelled(options, Av3CommitPipelineStep.FlushMetadataRoot, cancellationToken);

                trace.Add(Av3CommitPipelineStep.RecordJournal);
                ThrowIfCancelled(options, Av3CommitPipelineStep.RecordJournal, cancellationToken);
                var journalRequest = new Av3JournalRecordRequest
                {
                    TransactionId = options.Plan.TransactionId,
                    PreviousGeneration = options.Plan.PreviousGeneration,
                    TargetGeneration = options.Plan.TargetGeneration,
                    TargetMetadataRootCiphertextDigest = meta.CiphertextDigest
                };
                var journalBytes = journalRecorder.BuildDigestOnlyJournal(journalRequest);
                await WriteDurableAsync(store, Av3DurableFileLayout.JournalRelative, journalBytes, options, Av3CommitPipelineStep.FlushJournal, cancellationToken)
                    .ConfigureAwait(false);
                snapshot.JournalFlushed = true;
                ThrowIfCancelled(options, Av3CommitPipelineStep.FlushJournal, cancellationToken);

                trace.Add(Av3CommitPipelineStep.WriteActivationHeader);
                ThrowIfCancelled(options, Av3CommitPipelineStep.WriteActivationHeader, cancellationToken);
                var headerState = headerCommitter.CommitHarnessThreeCopy(
                    options.Plan,
                    meta,
                    options.Crypto.Vmk,
                    options.HarnessCipherSuiteId);
                snapshot.HeaderCopyDurableCount = Av3CommitHeaderCommitter.CountDurable(headerState);
                snapshot.HeaderCopyConflict = options.Simulation.HeaderCopyConflict || headerState.Copy1ConflictsWithCopy2;
                snapshot.RedundancyDegraded = snapshot.HeaderCopyDurableCount == 1;
                snapshot.ActivationWritten = true;

                var activationHeader = store.RereadRelative(Av3DurableFileLayout.HeaderCopy0)
                    ?? Av3HarnessCommitCrypto.BuildActivationHeaderCopy(
                        options.Crypto.Vmk,
                        options.Plan,
                        meta.CiphertextDigest,
                        meta.PlaintextCommitment,
                        copyIndex: 0,
                        options.HarnessCipherSuiteId);
                await WriteDurableAsync(
                        store,
                        Av3DurableFileLayout.ActivationRelative,
                        activationHeader,
                        options,
                        Av3CommitPipelineStep.FlushActivationHeader,
                        cancellationToken)
                    .ConfigureAwait(false);
                snapshot.ActivationFlushed = true;
                ThrowIfCancelled(options, Av3CommitPipelineStep.FlushActivationHeader, cancellationToken);

                trace.Add(Av3CommitPipelineStep.PostFlushReread);
                if (options.Simulation.FailReread)
                {
                    snapshot.RereadFailed = true;
                    return Finish(recovery, snapshot, trace, committed: false);
                }

                var headerRead = store.RereadRelative(Av3DurableFileLayout.ActivationRelative);
                var metaRead = store.RereadRelative(Av3DurableFileLayout.MetadataRelative);
                snapshot.RereadSucceeded = headerRead is not null && metaRead is not null;

                trace.Add(Av3CommitPipelineStep.PostFlushAuthentication);
                if (!snapshot.RereadSucceeded || options.Simulation.FailAuthentication)
                {
                    snapshot.ActivationAuthenticated = false;
                    snapshot.MetadataAuthenticated = false;
                    return Finish(recovery, snapshot, trace, committed: false);
                }

                var auth = Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(
                    headerRead!,
                    metaRead!,
                    options.Crypto.Vmk);
                if (!auth.Success)
                {
                    snapshot.ActivationAuthenticated = false;
                    snapshot.MetadataAuthenticated = false;
                    return Finish(recovery, snapshot, trace, committed: false);
                }

                snapshot.ActivationAuthenticated = true;
                snapshot.MetadataAuthenticated = true;

                trace.Add(Av3CommitPipelineStep.CommitClassification);
                trace.Add(Av3CommitPipelineStep.Cleanup);
                ThrowIfCancelled(options, Av3CommitPipelineStep.Cleanup, cancellationToken);
                if (options.Simulation.FailCleanup)
                {
                    snapshot.CleanupFailed = true;
                    snapshot.CleanupCompleted = false;
                    snapshot.FaultPoint = Av3FaultPoint.DuringCleanup;
                }
                else
                {
                    snapshot.CleanupCompleted = true;
                    _ = Av3CommitHarnessCleanup.TryRunOnce(options.VaultRoot, static () => { });
                }

                var (classification, repair) = recovery.ClassifySnapshot(snapshot);
                var postAuthTrusted = auth.Success;
                var cleanupPosture = Av3CommitCleanupResolver.Resolve(snapshot, postAuthTrusted);
                var wouldOpen = snapshot.HeaderCopyDurableCount != 1;
                _ = wouldOpen;
                var committed = postAuthTrusted
                    && snapshot.CleanupCompleted
                    && !snapshot.CleanupFailed
                    && classification is Av3RecoveryClassification.NewGenerationOpen
                        or Av3RecoveryClassification.RedundancyDegraded;

                return new Av3CommitPipelineResult
                {
                    Classification = classification,
                    Repair = repair,
                    Committed = committed,
                    PostAuthDataTrusted = postAuthTrusted,
                    CleanupPosture = cleanupPosture,
                    Trace = trace,
                    Snapshot = snapshot
                };
            }
            catch (OperationCanceledException)
            {
                snapshot.Aborted = true;
                return FinishCancelled(recovery, snapshot, trace, options.Simulation.CancelAfterStep
                    ?? options.Simulation.CancelBeforeFlushAtStep);
            }
            catch
            {
                snapshot.Aborted = true;
                return Finish(recovery, snapshot, trace, committed: false);
            }
        }
        finally
        {
            Av3WriterCommitGuard.ClearVaultHarnessState(options.VaultRoot);
        }
    }

    private static void ThrowIfCancelled(
        Av3CommitHarnessOptions options,
        Av3CommitPipelineStep step,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Simulation.CancelAfterStep == step)
        {
            throw new OperationCanceledException(step.ToString());
        }
    }

    private static Av3CommitPipelineResult FinishCancelled(
        Av3CommitRecoveryManager recovery,
        Av3CommitSnapshot snapshot,
        Av3CommitTrace trace,
        Av3CommitPipelineStep? step)
    {
        var finished = Finish(recovery, snapshot, trace, committed: false);
        var report = new Av3WriterCancellationReport { CancelledAtStep = step };
        return new Av3CommitPipelineResult
        {
            Classification = finished.Classification,
            Repair = finished.Repair,
            Committed = false,
            PostAuthDataTrusted = finished.PostAuthDataTrusted,
            CleanupPosture = finished.CleanupPosture,
            Trace = finished.Trace,
            Snapshot = finished.Snapshot,
            Cancelled = true,
            Cancellation = report
        };
    }

    private static Av3CommitPipelineResult Finish(
        Av3CommitRecoveryManager recovery,
        Av3CommitSnapshot snapshot,
        Av3CommitTrace trace,
        bool committed)
    {
        var (classification, repair) = recovery.ClassifySnapshot(snapshot);
        var postAuthTrusted = snapshot.ActivationAuthenticated && snapshot.MetadataAuthenticated;
        return new Av3CommitPipelineResult
        {
            Classification = classification,
            Repair = repair,
            Committed = committed,
            PostAuthDataTrusted = postAuthTrusted,
            CleanupPosture = Av3CommitCleanupResolver.Resolve(snapshot, postAuthTrusted),
            Trace = trace,
            Snapshot = snapshot
        };
    }

    private static async Task WriteDurableAsync(
        Av3CommitDurableStore store,
        string relative,
        byte[] payload,
        Av3CommitHarnessOptions options,
        Av3CommitPipelineStep flushStep,
        CancellationToken cancellationToken)
    {
        store.MaybeFailFlush(flushStep);
        if (options.Simulation.CancelBeforeFlushAtStep == flushStep)
        {
            throw new OperationCanceledException(flushStep.ToString());
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await store.WriteTempThenCommitAsync(
            relative,
            payload,
            new Av3DurableCommitOptions { TransactionId = options.Plan.TransactionId, TargetGeneration = options.Plan.TargetGeneration },
            cancellationToken).ConfigureAwait(false);
        if (!result.Durable)
        {
            throw new InvalidOperationException("durable_write_failed");
        }
    }
}