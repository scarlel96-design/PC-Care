using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;
using SmartPerformanceDoctor.AstraVault.Journal;

namespace SmartPerformanceDoctor.AstraVault.Durable;

/// <summary>Isolated durable write harness (test-only; not production writer).</summary>
public static class Av3DurableStorageHarness
{
    public static void RunCommitUntilMarker(
        string vaultRoot,
        Av3FaultPoint killMarker,
        Av3WritePlan plan,
        Av3HarnessCommitContext context)
    {
        Av3TestStorage.ValidateRelativePath(Av3DurableFileLayout.ManifestRelative);
        Directory.CreateDirectory(vaultRoot);
        var manifest = new Av3DurableManifest
        {
            PreviousGeneration = plan.PreviousGeneration,
            TargetGeneration = plan.TargetGeneration
        };

        void Progress(string step)
        {
            manifest.ProgressSteps.Add(step);
            manifest.Save(vaultRoot);
            var progressPath = Path.Combine(vaultRoot, Av3DurableFileLayout.ProgressRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(progressPath)!);
            File.WriteAllText(progressPath, step);
        }

        void Checkpoint(Av3FaultPoint point)
        {
            if (point == killMarker)
            {
                manifest.Save(vaultRoot);
                var markerPath = Path.Combine(vaultRoot, Av3DurableFileLayout.KillMarkerReachedRelative);
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
                var markerText = ((int)point).ToString(System.Globalization.CultureInfo.InvariantCulture);
                File.WriteAllText(markerPath, markerText);

                Thread.Sleep(Timeout.Infinite);
            }
        }

        var vmk = context.Vmk;
        Progress("prepare");
        Checkpoint(Av3FaultPoint.BeforeObjectWrite);

        var objectCipher = Av3HarnessCommitCrypto.EncryptHarnessObject(vmk, plan.TargetGeneration, context.HarnessObjectPlaintext);
        WriteAndMaybeFlush(vaultRoot, Av3DurableFileLayout.ObjectsRelative, objectCipher, manifest);
        Progress("write_objects");
        Checkpoint(Av3FaultPoint.AfterObjectWriteBeforeFlush);
        manifest.FlushedRelativePaths.Add(Av3DurableFileLayout.ObjectsRelative);
        manifest.Save(vaultRoot);
        Progress("flush_objects");
        Checkpoint(Av3FaultPoint.AfterObjectFlush);

        Checkpoint(Av3FaultPoint.BeforeMetadataWrite);
        var meta = Av3HarnessCommitCrypto.BuildMetadataRootArtifacts(vmk, plan);
        WriteAndMaybeFlush(vaultRoot, Av3DurableFileLayout.MetadataRelative, meta.Envelope, manifest);
        Progress("write_metadata");
        Checkpoint(Av3FaultPoint.AfterMetadataWriteBeforeFlush);
        manifest.FlushedRelativePaths.Add(Av3DurableFileLayout.MetadataRelative);
        manifest.Save(vaultRoot);
        Progress("flush_metadata");
        Checkpoint(Av3FaultPoint.AfterMetadataFlush);

        Checkpoint(Av3FaultPoint.BeforeJournalWrite);
        var journal = BuildJournal(plan, SHA256.HashData(objectCipher), SHA256.HashData(meta.Envelope), meta.CiphertextDigest);
        WriteAndMaybeFlush(vaultRoot, Av3DurableFileLayout.JournalRelative, journal.Write(), manifest);
        Progress("write_journal");
        Checkpoint(Av3FaultPoint.AfterJournalWriteBeforeFlush);
        manifest.FlushedRelativePaths.Add(Av3DurableFileLayout.JournalRelative);
        manifest.Save(vaultRoot);
        Progress("flush_journal");
        Checkpoint(Av3FaultPoint.AfterJournalFlush);

        Checkpoint(Av3FaultPoint.BeforeActivationHeaderWrite);
        var activation = Av3HarnessCommitCrypto.BuildActivationHeaderCopy(vmk, plan, meta.CiphertextDigest, meta.PlaintextCommitment);
        WriteAndMaybeFlush(vaultRoot, Av3DurableFileLayout.ActivationRelative, activation, manifest);
        Progress("write_activation");
        Checkpoint(Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush);
        manifest.FlushedRelativePaths.Add(Av3DurableFileLayout.ActivationRelative);
        manifest.Save(vaultRoot);
        Progress("flush_activation");
        Checkpoint(Av3FaultPoint.AfterActivationFlushBeforeReread);

        var headerBytes = File.ReadAllBytes(Path.Combine(vaultRoot, Av3DurableFileLayout.ActivationRelative));
        var metaBytes = File.ReadAllBytes(Path.Combine(vaultRoot, Av3DurableFileLayout.MetadataRelative));
        manifest.RereadSucceeded = headerBytes.Length > 0 && metaBytes.Length > 0;
        manifest.Save(vaultRoot);
        Progress("reread_activation");
        Checkpoint(Av3FaultPoint.AfterRereadBeforeAuthentication);

        var auth = Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(headerBytes, metaBytes, vmk);
        manifest.ActivationAuthenticated = auth.ActivationAeadAuthenticated && auth.Success;
        manifest.MetadataAuthenticated = auth.MetadataRootAeadAuthenticated && auth.Success;
        manifest.Save(vaultRoot);
        Progress("authenticate");
        Checkpoint(Av3FaultPoint.AfterAuthenticationBeforeCleanup);

        Checkpoint(Av3FaultPoint.DuringCleanup);
        manifest.CleanupCompleted = true;
        manifest.Save(vaultRoot);
        Progress("cleanup");
    }

    public static Av3CommitSnapshot BuildSnapshotFromManifest(string vaultRoot)
    {
        var m = Av3DurableManifest.Load(vaultRoot);
        var authFromProgress = m.ProgressSteps.Contains("authenticate");
        return new Av3CommitSnapshot
        {
            PreviousAuthenticatedGeneration = m.PreviousGeneration,
            AttemptedTargetGeneration = m.TargetGeneration,
            ObjectsFlushed = m.FlushedRelativePaths.Contains(Av3DurableFileLayout.ObjectsRelative),
            MetadataFlushed = m.FlushedRelativePaths.Contains(Av3DurableFileLayout.MetadataRelative),
            JournalFlushed = m.FlushedRelativePaths.Contains(Av3DurableFileLayout.JournalRelative),
            ActivationFlushed = m.FlushedRelativePaths.Contains(Av3DurableFileLayout.ActivationRelative),
            ActivationWritten = File.Exists(Path.Combine(vaultRoot, Av3DurableFileLayout.ActivationRelative)),
            RereadSucceeded = m.RereadSucceeded || m.ProgressSteps.Contains("reread_activation"),
            ActivationAuthenticated = m.ActivationAuthenticated || authFromProgress,
            MetadataAuthenticated = m.MetadataAuthenticated || authFromProgress,
            CleanupCompleted = m.CleanupCompleted,
            HeaderCopyDurableCount = m.HeaderCopyDurableCount,
            HeaderCopyConflict = m.HeaderCopyConflict
        };
    }

    public static Av3DurableWriteResult FlushRereadAuthenticate(
        string vaultRoot,
        string relativePath,
        ReadOnlySpan<byte> expectedWritten,
        ReadOnlySpan<byte> vmk,
        ReadOnlySpan<byte> headerForAuth,
        ReadOnlySpan<byte> metadataForAuth)
    {
        var abs = Path.Combine(vaultRoot, relativePath);
        using var handle = new Av3DurableWriteHandle(abs, relativePath);
        var flushed = Av3DurableFlush.TryFlush(handle);
        if (!flushed)
        {
            return new Av3DurableWriteResult { RelativePath = relativePath, FlushSucceeded = false };
        }

        var reread = File.ReadAllBytes(abs);
        var matched = reread.AsSpan().SequenceEqual(expectedWritten);
        var authOk = matched && Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(headerForAuth, metadataForAuth, vmk).Success;
        return new Av3DurableWriteResult
        {
            RelativePath = relativePath,
            FlushSucceeded = true,
            RereadMatched = matched,
            AuthenticationSucceeded = authOk
        };
    }

    private static void WriteAndMaybeFlush(string vaultRoot, string relative, byte[] data, Av3DurableManifest manifest)
    {
        Av3TestStorage.ValidateRelativePath(relative);
        var abs = Path.Combine(vaultRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllBytes(abs, data);
        manifest.Save(vaultRoot);
    }

    private static Av3JournalDescriptor BuildJournal(
        Av3WritePlan plan,
        byte[] objectDigest,
        byte[] metadataDigest,
        byte[] targetMetaRootDigest)
    {
        return new Av3JournalDescriptor
        {
            CipherSuiteId = 1,
            ContainerId = plan.ContainerId,
            TransactionId = plan.TransactionId,
            PreviousGeneration = plan.PreviousGeneration,
            TargetGeneration = plan.TargetGeneration,
            PreviousMetadataRootCiphertextDigest = plan.PreviousMetadataRootDigest,
            TargetMetadataRootCiphertextDigest = targetMetaRootDigest,
            ObjectWriteSetDigest = objectDigest,
            MetadataWriteDigest = metadataDigest,
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };
    }
}