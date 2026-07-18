using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Journal;

namespace SmartPerformanceDoctor.AstraVault.Experimental;

/// <summary>Disabled experimental writer — isolated test storage only (Phase E-2 crypto-linked FI harness).</summary>
public static class Av3ExperimentalWriter
{
    public static Av3CommitResult SimulateCommit(
        Av3WriteTransaction transaction,
        Av3FaultInjector? injector = null)
    {
        Av3ExperimentalWriterAccess.EnsureHarnessOnly(transaction.TestHarnessInvocation);
        var trace = new Av3WriteTrace();
        var snapshot = new Av3CommitSnapshot
        {
            PreviousAuthenticatedGeneration = transaction.Plan.PreviousGeneration,
            AttemptedTargetGeneration = transaction.Plan.TargetGeneration
        };

        try
        {
            RunCommitFlow(transaction, injector, trace, snapshot);
            snapshot.CleanupCompleted = true;
            var classification = Av3RecoveryClassifier.Classify(snapshot);
            return new Av3CommitResult
            {
                Completed = true,
                Classification = classification,
                FaultResult = new Av3FaultInjectionResult
                {
                    CommitCompleted = true,
                    Classification = classification,
                    Trace = trace,
                    TrustedOpenGeneration = transaction.Plan.TargetGeneration,
                    MetadataTrusted = Av3RecoveryClassifier.TrustsMetadata(classification),
                    ActivationAuthenticated = snapshot.ActivationAuthenticated
                }
            };
        }
        catch (Av3SimulatedFaultException ex)
        {
            snapshot.FaultPoint = ex.FaultPoint;
            var classification = Av3RecoveryClassifier.Classify(snapshot);
            return new Av3CommitResult
            {
                Completed = false,
                Classification = classification,
                FaultResult = new Av3FaultInjectionResult
                {
                    CommitCompleted = false,
                    InjectedFault = ex.FaultPoint,
                    Classification = classification,
                    Trace = trace,
                    TrustedOpenGeneration = classification == Av3RecoveryClassification.NewGenerationOpen
                        ? transaction.Plan.TargetGeneration
                        : transaction.Plan.PreviousGeneration,
                    MetadataTrusted = false,
                    ActivationAuthenticated = snapshot.ActivationAuthenticated
                }
            };
        }
        catch (Av3SimulatedIoException)
        {
            snapshot.Aborted = true;
            return new Av3CommitResult
            {
                Completed = false,
                Classification = Av3RecoveryClassification.Aborted,
                FaultResult = new Av3FaultInjectionResult
                {
                    CommitCompleted = false,
                    Classification = Av3RecoveryClassification.Aborted,
                    Trace = trace,
                    TrustedOpenGeneration = transaction.Plan.PreviousGeneration,
                    MetadataTrusted = false
                }
            };
        }
    }

    private static void RunCommitFlow(
        Av3WriteTransaction txn,
        Av3FaultInjector? injector,
        Av3WriteTrace trace,
        Av3CommitSnapshot snapshot)
    {
        injector ??= new Av3FaultInjector();
        var vmk = txn.HarnessContext.Vmk;
        Av3HarnessMetadataArtifacts? metadataArtifacts = null;
        byte[]? objectCipher = null;

        trace.Record("prepare");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.BeforeObjectWrite, trace);
        objectCipher = Av3HarnessCommitCrypto.EncryptHarnessObject(
            vmk,
            txn.Plan.TargetGeneration,
            txn.HarnessContext.HarnessObjectPlaintext);
        txn.Storage.WritePending("objects/set.bin", objectCipher);
        trace.Record("write_objects");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterObjectWriteBeforeFlush, trace);
        Av3FlushFaultHarness.RequireFlushOrAbort(
            txn.Storage, "objects/set.bin", injector, Av3FaultPoint.AfterObjectWriteBeforeFlush, snapshot);
        snapshot.ObjectsFlushed = true;
        trace.Record("flush_objects");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterObjectFlush, trace);

        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.BeforeMetadataWrite, trace);
        metadataArtifacts = Av3HarnessCommitCrypto.BuildMetadataRootArtifacts(vmk, txn.Plan);
        txn.Storage.WritePending("metadata/root.enc", metadataArtifacts.Envelope);
        trace.Record("write_metadata");
        trace.Record("metadata_root_digest");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterMetadataWriteBeforeFlush, trace);
        Av3FlushFaultHarness.RequireFlushOrAbort(
            txn.Storage, "metadata/root.enc", injector, Av3FaultPoint.AfterMetadataWriteBeforeFlush, snapshot);
        snapshot.MetadataFlushed = true;
        trace.Record("flush_metadata");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterMetadataFlush, trace);

        var objectDigest = SHA256.HashData(objectCipher);
        var metadataWriteDigest = SHA256.HashData(metadataArtifacts.Envelope);

        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.BeforeJournalWrite, trace);
        var journal = BuildJournal(
            txn,
            objectDigest,
            metadataWriteDigest,
            metadataArtifacts.CiphertextDigest);
        var journalBytes = journal.Write();
        VerifyJournalRecordDigest(journalBytes);
        txn.Storage.WritePending("journal/current.jnal", journalBytes);
        trace.Record("write_journal");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterJournalWriteBeforeFlush, trace);
        Av3FlushFaultHarness.RequireFlushOrAbort(
            txn.Storage, "journal/current.jnal", injector, Av3FaultPoint.AfterJournalWriteBeforeFlush, snapshot);
        snapshot.JournalFlushed = true;
        trace.Record("flush_journal");
        trace.Record("journal_digest_verified");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterJournalFlush, trace);

        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.BeforeActivationHeaderWrite, trace);
        var activationHeader = Av3HarnessCommitCrypto.BuildActivationHeaderCopy(
            vmk,
            txn.Plan,
            metadataArtifacts.CiphertextDigest,
            metadataArtifacts.PlaintextCommitment);
        txn.Storage.WritePending("header/activation.bin", activationHeader);
        snapshot.ActivationWritten = true;
        trace.Record("write_activation");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush, trace);
        Av3FlushFaultHarness.RequireFlushOrAbort(
            txn.Storage, "header/activation.bin", injector, Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush, snapshot);
        injector.ArmPostFlushTruncation(Av3FaultPoint.AfterActivationFlushBeforeReread, txn.Storage);
        snapshot.ActivationFlushed = true;
        trace.Record("flush_activation");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterActivationFlushBeforeReread, trace);

        if (injector.ShouldFailReread())
        {
            snapshot.RereadSucceeded = false;
            snapshot.RereadFailed = true;
            throw new Av3SimulatedFaultException(Av3FaultPoint.AfterActivationFlushBeforeReread);
        }

        var rereadHeader = txn.Storage.TryReadFlushed("header/activation.bin")
            ?? throw new Av3SimulatedFaultException(Av3FaultPoint.AfterActivationFlushBeforeReread);
        var rereadMetadata = txn.Storage.TryReadFlushed("metadata/root.enc")
            ?? throw new Av3SimulatedFaultException(Av3FaultPoint.AfterActivationFlushBeforeReread);
        snapshot.RereadSucceeded = true;
        trace.Record("reread_activation");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterRereadBeforeAuthentication, trace);

        var auth = Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(rereadHeader, rereadMetadata, vmk);
        if (injector.ShouldFailAuthentication())
        {
            auth = auth with { Success = false };
        }

        if (!auth.Success)
        {
            snapshot.ActivationAuthenticated = auth.ActivationAeadAuthenticated;
            snapshot.MetadataAuthenticated = auth.MetadataRootAeadAuthenticated
                                             && auth.MetadataPlaintextCanonicalValidated
                                             && auth.RootPlaintextCommitmentVerified
                                             && auth.GenerationRollbackValidated;
            throw new Av3SimulatedFaultException(Av3FaultPoint.AfterRereadBeforeAuthentication);
        }

        snapshot.ActivationAuthenticated = true;
        snapshot.MetadataAuthenticated = true;
        trace.Record("authenticate");
        trace.Record("generation_rollback_validated");
        trace.Record("classified_committed");
        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.AfterAuthenticationBeforeCleanup, trace);

        Av3ProcessKillHarness.SimulateKillAtStep(injector, Av3FaultPoint.DuringCleanup, trace);
        if (injector.ShouldAbortCleanup())
        {
            snapshot.CleanupFailed = true;
            snapshot.CleanupCompleted = false;
            throw new Av3SimulatedFaultException(Av3FaultPoint.DuringCleanup);
        }

        trace.Record("cleanup");
    }

    private static void VerifyJournalRecordDigest(ReadOnlySpan<byte> journalBytes)
    {
        var parsed = Av3JournalDescriptor.Parse(journalBytes);
        var digest = Av3JournalDigest.ComputeRecordDigest(journalBytes);
        if (!digest.AsSpan().SequenceEqual(parsed.RecordDigest))
        {
            throw new CryptographicException("Journal record digest mismatch.");
        }
    }

    private static Av3JournalDescriptor BuildJournal(
        Av3WriteTransaction txn,
        ReadOnlySpan<byte> objectWriteSetDigest,
        ReadOnlySpan<byte> metadataWriteDigest,
        ReadOnlySpan<byte> targetMetadataRootCiphertextDigest)
    {
        return new Av3JournalDescriptor
        {
            CipherSuiteId = 1,
            ContainerId = txn.Plan.ContainerId,
            TransactionId = txn.Plan.TransactionId,
            PreviousGeneration = txn.Plan.PreviousGeneration,
            TargetGeneration = txn.Plan.TargetGeneration,
            PreviousMetadataRootCiphertextDigest = txn.Plan.PreviousMetadataRootDigest,
            TargetMetadataRootCiphertextDigest = targetMetadataRootCiphertextDigest.ToArray(),
            ObjectWriteSetDigest = objectWriteSetDigest.ToArray(),
            MetadataWriteDigest = metadataWriteDigest.ToArray(),
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };
    }
}