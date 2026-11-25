namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record RecoveryFileObservation(string Path, string State, bool BackupVerified);
public sealed record RecoveryReconciliation(string State, string Code, string TransactionId,
    bool OriginalBytesVerified, bool CanRequestRecovery, IReadOnlyList<RecoveryFileObservation> Files);

public sealed partial class UpdateTransaction
{
    /// <summary>Observe authenticated pending inputs. This method never writes or grants execution authority.</summary>
    public RecoveryReconciliation ReconcileRecovery(string installRoot) => ReconcileRecoveryCore(installRoot, false);

    private RecoveryReconciliation ReconcileRecoveryCore(string installRoot, bool gateHeld)
    {
        var observations = new List<RecoveryFileObservation>();
        string id = "";
        try
        {
            installRoot = Path.GetFullPath(installRoot);
            using var install = WindowsFileBoundary.PinDirectory(installRoot);
            if (!Directory.Exists(_workRoot)) return new("NotStarted", "NO-PENDING", "", false, false, observations);
            using var work = WindowsFileBoundary.PinDirectory(_workRoot);
            if (_strictAcl) VerifyProtectedAcl(_workRoot);
            var status = gateHeld ? new UpdateRecoveryStatus(UpdateRecoveryState.RecoveryRequired, "PENDING", "") : InspectRecovery(installRoot);
            if (status.State == UpdateRecoveryState.RecoveryInProgress)
                return new("RecoveryDeferred", "LOCKED", status.TransactionId, false, false, observations);
            var pending = new List<(string Directory, UpdateJournal Journal)>();
            var scanned = 0;
            foreach (var dir in Directory.EnumerateDirectories(_workRoot, "tx-*"))
            {
                if (++scanned > 4096) return new("ManualReviewRequired", "HISTORY-LIMIT", "", false, false, observations);
                if (!TryReadTrustedJournal(dir, installRoot, out var journal, out var reason) || journal is null)
                    return new("ManualReviewRequired", reason, "", false, false, observations);
                if (journal.Stage is not (UpdateStage.Committed or UpdateStage.Restored)) pending.Add((dir, journal));
                if (pending.Count > 1) return new("ManualReviewRequired", "MULTIPLE-PENDING-TRANSACTIONS", "", false, false, observations);
            }
            if (pending.Count == 0)
            {
                if (!gateHeld && status.State is UpdateRecoveryState.RecoveryRequired or UpdateRecoveryState.RecoveryFailed)
                    return new("ManualReviewRequired", status.Code, status.TransactionId, false, false, observations);
                return new("NoPendingTransaction", status.Code, status.TransactionId, false, false, observations);
            }
            var (directory, record) = pending[0]; id = record.TransactionId;
            if (record.SchemaVersion != 2 || record.Stage < UpdateStage.Applying)
                return new("ManualReviewRequired", "RECOVERY-SCOPE-INCOMPLETE", id, false, false, observations);
            using var transaction = WindowsFileBoundary.PinDirectory(directory);
            if (_strictAcl) VerifyProtectedAcl(directory);
            using var package = WindowsFileBoundary.OpenRead(directory, "package.spdup");
            VerifyProtectedFile(package, directory);
            var signed = SignedPackage.Verify(package.Stream, record.FromVersion, _policy);
            if (!signed.CanApply || signed.Manifest is null || signed.Manifest.PackageId != record.PackageId ||
                signed.Manifest.ToVersion != record.ToVersion || signed.Manifest.Files.Count != record.Files.Count)
                return new("ManualReviewRequired", "RECOVERY-PACKAGE-REJECTED", id, false, false, observations);
            var signedFiles = signed.Manifest.Files.ToDictionary(x => x.Path, StringComparer.Ordinal);
            var held = new List<WindowsFileBoundary.FileLease>();
            try
            {
                foreach (var entry in record.Files)
                {
                    if (!signedFiles.TryGetValue(entry.Path, out var expected) || expected.Length != entry.NewLength ||
                        !string.Equals(expected.Sha256, entry.NewSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("RECOVERY-SIGNED-SCOPE");
                    if (!entry.Existed)
                    {
                        if (File.Exists(Resolve(installRoot, Relative(entry.Path))))
                        {
                            var target = WindowsFileBoundary.OpenRead(installRoot, Relative(entry.Path)); held.Add(target);
                            target.Verify(expected.Length, expected.Sha256, entry.InstalledIdentity);
                            observations.Add(new(entry.Path, "UpdatedVerified", true));
                        }
                        else observations.Add(new(entry.Path, "NewFileAbsent", true));
                        continue;
                    }
                    var backup = WindowsFileBoundary.OpenRead(Path.Combine(directory, "backup"), Relative(entry.Path)); held.Add(backup);
                    VerifyProtectedFile(backup, Path.Combine(directory, "backup"));
                    backup.Verify(entry.OriginalLength, entry.OriginalSha256, entry.BackupIdentity);
                    var current = WindowsFileBoundary.OpenRead(installRoot, Relative(entry.Path)); held.Add(current);
                    current.Validate(entry.OriginalIdentity);
                    if (_strictAcl) ValidateProtectedSecurity(current.ReadSecurity(), false);
                    var hash = current.Hash();
                    var state = current.Stream.Length == entry.OriginalLength && hash == entry.OriginalSha256 ? "OriginalVerified" :
                        current.Stream.Length == expected.Length && hash == expected.Sha256 ? "UpdatedVerified" : "UnknownContent";
                    observations.Add(new(entry.Path, state, true));
                }
                foreach (var file in held) file.Validate();
                install.Validate(); work.Validate(); transaction.Validate(); package.Validate();
                var unknown = observations.Any(x => x.State == "UnknownContent");
                var originals = observations.All(x => x.State is "OriginalVerified" or "NewFileAbsent");
                return new(unknown ? "ManualReviewRequired" : "RecoveryRequired",
                    unknown ? "RECOVERY-CONTENT-REVIEW" : originals ? "ORIGINAL-BYTES-PRESERVED-PENDING" : "RECOVERY-INPUTS-VERIFIED",
                    id, originals, !unknown, observations);
            }
            finally { foreach (var file in held) file.Dispose(); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            var diagnostic = UpdateFailureDiagnostic.Capture(ex, "Reconciliation", UpdateStage.RecoveryRequired, "", "", "", _strictAcl, false);
            return new(diagnostic.NativeErrorCode == 32 ? "RecoveryDeferred" : "ManualReviewRequired",
                diagnostic.NativeErrorCode == 32 ? "RECOVERY-SHARING-VIOLATION" : "RECOVERY-EVIDENCE-REJECTED", id, false, false, observations);
        }
    }
}
