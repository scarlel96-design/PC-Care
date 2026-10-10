using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;

public enum UpdateStage
{
    Prepared, PublisherVerified, Staged, BackupVerified, Applying,
    PostApplyVerification, Committed, RollbackPending, RollingBack, Restored, RecoveryRequired
}

public sealed class UpdateJournal
{
    public int SchemaVersion { get; set; } = 2;
    public string InstallIdentity { get; set; } = "";
    public string TransactionIdentity { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string InstallRoot { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string FromVersion { get; set; } = "";
    public string ToVersion { get; set; } = "";
    public UpdateStage Stage { get; set; }
    public string Failure { get; set; } = "";
    public List<UpdateFileJournal> Files { get; set; } = [];
}

public sealed class UpdateFileJournal
{
    public string OriginalIdentity { get; set; } = "";
    public string BackupIdentity { get; set; } = "";
    public string InstalledIdentity { get; set; } = "";
    public string TemporaryPath { get; set; } = "";
    public bool TemporaryReady { get; set; }
    public string Path { get; set; } = "";
    public bool Existed { get; set; }
    public long OriginalLength { get; set; }
    public string OriginalSha256 { get; set; } = "";
    public string NewSha256 { get; set; } = "";
    public bool Applied { get; set; }
    public bool Restored { get; set; }
}

public sealed record TransactionResult(UpdateStage Stage, string Code, string TransactionId, int FilesApplied)
{
    public string Failure { get; init; } = "";
}

public enum UpdateRecoveryState
{
    NoPendingTransaction, RecoveryAvailable, RecoveryInProgress,
    RecoveryCompleted, RecoveryRequired, RecoveryFailed
}

public enum UpdateFaultPoint
{
    AfterPackageVerification, AfterStagingCreated, DuringStageExtraction,
    DuringBackupCreation, AfterBackupVerification, AfterApplyingJournal,
    AfterFirstReplacement, BeforeReplacement, AfterLastReplacement,
    DuringPostApplyVerification, BeforeCommittedJournal, AfterRollbackStarted,
    DuringRollbackCopy, BeforeRestoredJournal, BeforeFailureJournal,
    AfterNewFileIntent, AfterNewFileCreated, AfterNewFileIdentity, BeforeNewFileRename, AfterNewFileRename,
    JournalTempCreated, JournalPartialWrite, JournalFlushed, JournalBeforeReplace, JournalAfterReplace
}

public sealed record UpdateRecoveryStatus(UpdateRecoveryState State, string Code, string TransactionId);

public sealed class UpdateTransaction
{
    private readonly PublisherTrustPolicy _policy;
    private readonly string _workRoot;
    private readonly bool _strictAcl;
    private Action<UpdateFaultPoint>? _journalFault;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const int MaxJournalFiles = 4096;
    private const int MaxJournalBytes = 1024 * 1024;
    private const int MaxFailureCharacters = 2048;

    public UpdateTransaction(PublisherTrustPolicy policy, string workRoot, bool strictAcl)
    {
        _policy = policy;
        _workRoot = Path.GetFullPath(workRoot);
        _strictAcl = strictAcl;
    }

    public void InitializeProtectedRoot()
    {
        if (!_strictAcl) throw new InvalidOperationException("protected mode required");
        SecureWorkRoot();
    }

    public TransactionResult Apply(string packagePath, string installRoot, string currentVersion,
        Action<int>? afterFileApplied = null, Func<string, bool>? postApplyVersionCheck = null,
        Action<UpdateFaultPoint>? faultInjector = null)
    {
        _journalFault = faultInjector;
        installRoot = Path.GetFullPath(installRoot);
        EnsureRoot(installRoot);
        if (_strictAcl) SecureWorkRoot();
        else Directory.CreateDirectory(_workRoot);
        EnsureRoot(_workRoot);
        using var installPin = WindowsFileBoundary.PinDirectory(installRoot);
        using var workPin = WindowsFileBoundary.PinDirectory(_workRoot);
        using var gate = WindowsFileBoundary.OpenExclusive(_workRoot, "update.lock", !File.Exists(Path.Combine(_workRoot, "update.lock")));
        VerifyProtectedFile(gate, _workRoot);
        var priorJournals = new List<UpdateJournal>();
        foreach (var dir in Directory.EnumerateDirectories(_workRoot, "tx-*"))
        {
            if (!TryReadTrustedJournal(dir, installRoot, out var prior, out var reason) || prior is null)
                return new(UpdateStage.RecoveryRequired, reason, "", 0);
            if (_strictAcl) { VerifyProtectedAcl(dir); AllowReadOnlyInspection(dir); }
            if (prior.Stage is not (UpdateStage.Committed or UpdateStage.Restored))
                return new(UpdateStage.RecoveryRequired, "PRIOR-TRANSACTION", prior.TransactionId, 0);
            if (prior.SchemaVersion == 1) ArchiveLegacyTerminal(dir, installRoot);
            priorJournals.Add(prior);
        }

        var fullPackage = Path.GetFullPath(packagePath);
        using var inputPackage = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(fullPackage)!, Path.GetFileName(fullPackage));
        var source = SignedPackage.Verify(inputPackage.Stream, currentVersion, _policy);
        if (!source.CanApply || source.Manifest is null)
            return new(UpdateStage.Prepared, source.Code, "", 0);
        if (!FitsRecoveryJournal(source.Manifest, installRoot, currentVersion))
            return new(UpdateStage.Prepared, "RECOVERY-JOURNAL-LIMIT", "", 0);
        faultInjector?.Invoke(UpdateFaultPoint.AfterPackageVerification);
        if (priorJournals.Any(prior => prior.Stage == UpdateStage.Committed &&
            string.Equals(prior.PackageId, source.Manifest.PackageId, StringComparison.OrdinalIgnoreCase)))
            return new(UpdateStage.Prepared, "REPLAY", "", 0);

        var id = Guid.NewGuid().ToString("N");
        var txDir = Path.Combine(_workRoot, "tx-" + id);
        Directory.CreateDirectory(txDir);
        EnsureRoot(txDir);
        if (_strictAcl) SetProtectedAcl(txDir);
        var journal = new UpdateJournal
        {
            TransactionId = id, PackageId = source.Manifest.PackageId,
            InstallRoot = installRoot, InstallIdentity = installPin.Identity,
            TransactionIdentity = DirectoryIdentity(txDir), FromVersion = currentVersion, Stage = UpdateStage.Prepared
        };
        WriteJournal(txDir, journal);
        var packageCopy = Path.Combine(txDir, "package.spdup");
        var stageDir = Path.Combine(txDir, "stage");
        var backupDir = Path.Combine(txDir, "backup");
        var retained = new List<WindowsFileBoundary.FileLease>();
        var packageIdentity = "";
        try
        {
            faultInjector?.Invoke(UpdateFaultPoint.AfterStagingCreated);
            using (var outputPackage = WindowsFileBoundary.OpenWrite(txDir, "package.spdup", create: true))
                outputPackage.CopyFrom(inputPackage);
            using var packageLease = WindowsFileBoundary.OpenRead(txDir, "package.spdup");
            VerifyProtectedFile(packageLease, txDir);
            packageIdentity = packageLease.Identity;
            var stagedVerification = SignedPackage.Verify(packageLease.Stream, currentVersion, _policy);
            if (!stagedVerification.CanApply || stagedVerification.Manifest is null)
            {
                journal.Stage = UpdateStage.Restored;
                journal.Failure = "STAGED-PACKAGE-FAIL: " + stagedVerification.Code;
                WriteJournal(txDir, journal);
                return new(UpdateStage.Restored, "STAGED-PACKAGE-FAIL", id, 0);
            }
            var manifest = stagedVerification.Manifest;
            if (!FitsRecoveryJournal(manifest, installRoot, currentVersion))
                throw new InvalidDataException("RECOVERY-JOURNAL-LIMIT");
            journal.ToVersion = manifest.ToVersion;
            journal.Stage = UpdateStage.PublisherVerified;
            WriteJournal(txDir, journal);
            Directory.CreateDirectory(stageDir);
            Directory.CreateDirectory(backupDir);
            packageLease.Stream.Position = 0;
            using (var archive = new ZipArchive(packageLease.Stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                foreach (var item in manifest.Files)
                {
                    var relative = Relative(item.Path);
                    var stageFile = Resolve(stageDir, relative);
                    using (var input = archive.GetEntry(item.Path)!.Open())
                    using (var output = WindowsFileBoundary.OpenWrite(stageDir, relative, create: true))
                    {
                        input.CopyTo(output.Stream);
                        output.Stream.Flush(flushToDisk: true);
                        output.Verify(item.Length, item.Sha256);
                    }
                    VerifyFile(stageFile, item.Length, item.Sha256);
                    faultInjector?.Invoke(UpdateFaultPoint.DuringStageExtraction);
                }
            }
            journal.Stage = UpdateStage.Staged;
            WriteJournal(txDir, journal);
            foreach (var item in manifest.Files)
            {
                var relative = Relative(item.Path);
                var target = Resolve(installRoot, relative);
                GuardExistingPath(installRoot, relative);
                var backup = Resolve(backupDir, relative);
                var entry = new UpdateFileJournal { Path = item.Path, NewSha256 = item.Sha256 };
                if (File.Exists(target))
                {
                    entry.Existed = true;
                    using var original = WindowsFileBoundary.OpenRead(installRoot, relative);
                    entry.OriginalIdentity = original.Identity;
                    entry.OriginalLength = original.Stream.Length;
                    entry.OriginalSha256 = original.Hash();
                    using var backupLease = WindowsFileBoundary.OpenWrite(backupDir, relative, create: true);
                    VerifyProtectedFile(backupLease, Path.GetDirectoryName(backup)!);
                    backupLease.CopyFrom(original);
                    entry.BackupIdentity = backupLease.Identity;
                    faultInjector?.Invoke(UpdateFaultPoint.DuringBackupCreation);
                    backupLease.Verify(entry.OriginalLength, entry.OriginalSha256);
                }
                journal.Files.Add(entry);
                if (entry.Existed) retained.Add(WindowsFileBoundary.OpenRead(backupDir, relative));
            }
            journal.Stage = UpdateStage.BackupVerified;
            WriteJournal(txDir, journal);
            faultInjector?.Invoke(UpdateFaultPoint.AfterBackupVerification);
            journal.Stage = UpdateStage.Applying;
            WriteJournal(txDir, journal); // durable before the first file mutation
            faultInjector?.Invoke(UpdateFaultPoint.AfterApplyingJournal);
            var prepared = new Dictionary<string, (WindowsFileBoundary.FileLease Source, WindowsFileBoundary.FileLease Target)>();
            foreach (var entry in journal.Files)
            {
                var relative = Relative(entry.Path);
                GuardExistingPath(installRoot, relative);
                var expected = manifest.Files.Single(f => f.Path == entry.Path);
                var sourceLease = WindowsFileBoundary.OpenRead(stageDir, relative);
                retained.Add(sourceLease);
                VerifyProtectedFile(sourceLease, Path.GetDirectoryName(Resolve(stageDir, relative))!);
                sourceLease.Verify(expected.Length, expected.Sha256);
                if (!entry.Existed)
                {
                    entry.TemporaryPath = Path.Combine(Path.GetDirectoryName(relative) ?? "",
                        ".pccare-" + id + "-" + Guid.NewGuid().ToString("N") + ".pending");
                    WriteJournal(txDir, journal);
                    faultInjector?.Invoke(UpdateFaultPoint.AfterNewFileIntent);
                }
                var targetLease = WindowsFileBoundary.OpenWrite(installRoot,
                    entry.Existed ? relative : entry.TemporaryPath, create: !entry.Existed, deletable: !entry.Existed);
                retained.Add(targetLease);
                if (!entry.Existed) faultInjector?.Invoke(UpdateFaultPoint.AfterNewFileCreated);
                if (entry.Existed) targetLease.Verify(entry.OriginalLength, entry.OriginalSha256, entry.OriginalIdentity);
                entry.InstalledIdentity = targetLease.Identity;
                WriteJournal(txDir, journal); // Record new-file identity before writing its content.
                if (!entry.Existed) faultInjector?.Invoke(UpdateFaultPoint.AfterNewFileIdentity);
                prepared.Add(entry.Path, (sourceLease, targetLease));
            }
            // Acquire every destination before changing any existing file.
            var count = 0;
            foreach (var entry in journal.Files)
            {
                var (sourceLease, targetLease) = prepared[entry.Path];
                var expected = manifest.Files.Single(file => file.Path == entry.Path);
                faultInjector?.Invoke(UpdateFaultPoint.BeforeReplacement);
                targetLease.CopyFrom(sourceLease);
                targetLease.Verify(expected.Length, expected.Sha256);
                if (!entry.Existed)
                {
                    entry.TemporaryReady = true;
                    WriteJournal(txDir, journal);
                    faultInjector?.Invoke(UpdateFaultPoint.BeforeNewFileRename);
                    targetLease.RenameOpenedFile(Resolve(installRoot, Relative(entry.Path)));
                    faultInjector?.Invoke(UpdateFaultPoint.AfterNewFileRename);
                }
                entry.Applied = true;
                WriteJournal(txDir, journal);
                count++;
                afterFileApplied?.Invoke(count);
                if (count == 1) faultInjector?.Invoke(UpdateFaultPoint.AfterFirstReplacement);
            }
            faultInjector?.Invoke(UpdateFaultPoint.AfterLastReplacement);
            journal.Stage = UpdateStage.PostApplyVerification;
            WriteJournal(txDir, journal);
            foreach (var item in manifest.Files)
            {
                faultInjector?.Invoke(UpdateFaultPoint.DuringPostApplyVerification);
                retained.Single(file => file.Identity == journal.Files.Single(entry => entry.Path == item.Path).InstalledIdentity)
                    .Verify(item.Length, item.Sha256);
            }
            if (postApplyVersionCheck is not null && !postApplyVersionCheck(manifest.ToVersion))
                throw new InvalidDataException("installed version postcondition");
            faultInjector?.Invoke(UpdateFaultPoint.BeforeCommittedJournal);
            journal.Stage = UpdateStage.Committed;
            WriteJournal(txDir, journal);
            // Keep the package handle pinned through commit. Cleanup occurs after it closes.
            return new(UpdateStage.Committed, "COMMITTED", id, count);
        }
        catch (Exception ex)
        {
            foreach (var file in retained) file.Dispose();
            retained.Clear();
            journal.Failure = ex.GetType().Name + ": " + ex.Message;
            journal.Stage = UpdateStage.RollbackPending;
            try { faultInjector?.Invoke(UpdateFaultPoint.BeforeFailureJournal); WriteJournal(txDir, journal); }
            catch (Exception journalError)
            {
                return FailureResult(journal, ex, journalError);
            }
            return RollbackCore(txDir, installRoot, journal, faultInjector);
        }
        finally
        {
            foreach (var file in retained) file.Dispose();
            if (journal.Stage == UpdateStage.Committed && packageIdentity.Length != 0)
            {
                try
                {
                    using var cleanup = WindowsFileBoundary.OpenWrite(txDir, "package.spdup", deletable: true);
                    cleanup.Validate(packageIdentity);
                    cleanup.DeleteOpenedFile();
                }
                catch (Exception cleanupError)
                {
                    Console.Error.WriteLine("UPDATE-CLEANUP-FAIL: " + cleanupError.Message);
                }
            }
        }
    }

    public TransactionResult Recover(string installRoot, Action<UpdateFaultPoint>? faultInjector = null)
    {
        _journalFault = faultInjector;
        installRoot = Path.GetFullPath(installRoot);
        EnsureRoot(installRoot);
        if (_strictAcl) SecureWorkRoot();
        else Directory.CreateDirectory(_workRoot);
        EnsureRoot(_workRoot);
        using var installPin = WindowsFileBoundary.PinDirectory(installRoot);
        using var workPin = WindowsFileBoundary.PinDirectory(_workRoot);
        using var gate = WindowsFileBoundary.OpenExclusive(_workRoot, "update.lock", !File.Exists(Path.Combine(_workRoot, "update.lock")));
        VerifyProtectedFile(gate, _workRoot);
        foreach (var dir in Directory.EnumerateDirectories(_workRoot, "tx-*").OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!TryReadTrustedJournal(dir, installRoot, out var journal, out var reason))
                return new(UpdateStage.RecoveryRequired, reason, "", 0);
            if (_strictAcl) { VerifyProtectedAcl(dir); AllowReadOnlyInspection(dir); }
            if (journal is null) return new(UpdateStage.RecoveryRequired, "JOURNAL-MISSING", "", 0);
            if (journal.Stage is UpdateStage.Committed or UpdateStage.Restored)
            {
                if (journal.SchemaVersion == 1) ArchiveLegacyTerminal(dir, installRoot);
                continue;
            }
            if (journal.Stage < UpdateStage.Applying)
            {
                journal.Stage = UpdateStage.Restored;
                WriteJournal(dir, journal);
                continue;
            }
            return RollbackCore(dir, installRoot, journal, faultInjector);
        }
        return new(UpdateStage.Restored, "NO-PENDING", "", 0);
    }

    /// <summary>Read-only startup probe. It never creates a lock, journal, or directory.</summary>
    public UpdateRecoveryStatus InspectRecovery(string installRoot)
    {
        installRoot = Path.GetFullPath(installRoot);
        if (!Directory.Exists(_workRoot))
            return new(UpdateRecoveryState.NoPendingTransaction, "NO-PENDING", "");
        try
        {
            EnsureRoot(_workRoot);
            if (_strictAcl) VerifyProtectedAcl(_workRoot);
            var lockPath = Path.Combine(_workRoot, "update.lock");
            if (File.Exists(lockPath))
            {
                try
                {
                    using var probe = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None);
                }
                catch (IOException)
                {
                    return new(UpdateRecoveryState.RecoveryInProgress, "LOCKED", "");
                }
            }
            var completed = false;
            foreach (var dir in Directory.EnumerateDirectories(_workRoot, "tx-*")
                         .OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!TryReadTrustedJournal(dir, installRoot, out var journal, out var reason))
                    return new(UpdateRecoveryState.RecoveryRequired, reason, "");
                if (_strictAcl) VerifyProtectedAcl(dir);
                if (journal is null)
                    return new(UpdateRecoveryState.RecoveryRequired, "JOURNAL-MISSING", "");
                if (journal.Stage == UpdateStage.Restored) completed = true;
                else if (journal.Stage == UpdateStage.RecoveryRequired &&
                         (journal.Failure?.Contains("rollback:", StringComparison.OrdinalIgnoreCase) ?? false))
                    return new(UpdateRecoveryState.RecoveryFailed, "ROLLBACK-FAIL", journal.TransactionId);
                else if (journal.Stage != UpdateStage.Committed)
                    return new(UpdateRecoveryState.RecoveryAvailable, "PENDING", journal.TransactionId);
            }
            return completed
                ? new(UpdateRecoveryState.RecoveryCompleted, "RESTORED", "")
                : new(UpdateRecoveryState.NoPendingTransaction, "NO-PENDING", "");
        }
        catch (UnauthorizedAccessException)
        {
            return new(UpdateRecoveryState.RecoveryRequired, "INSPECTION-ACCESS-DENIED", "");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            return new(UpdateRecoveryState.RecoveryRequired, "INSPECTION-FAIL", "");
        }
    }

    private bool TryReadTrustedJournal(string directory, string installRoot,
        out UpdateJournal? journal, out string reason)
    {
        journal = null;
        reason = "JOURNAL-INVALID";
        try
        {
            EnsureRoot(directory);
            var name = Path.GetFileName(directory);
            if (!name.StartsWith("tx-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[3..], "N", out _)) return false;
            var path = Path.Combine(directory, "journal.json");
            if ((!File.Exists(path) && !File.Exists(Path.Combine(directory, "journal.previous.json"))) ||
                (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
            {
                reason = "JOURNAL-MISSING";
                return false;
            }
            journal = ReadJournal(directory);
            if (journal is null || !string.Equals(name, "tx-" + journal.TransactionId, StringComparison.Ordinal) ||
                !string.Equals(journal.InstallRoot, installRoot, StringComparison.OrdinalIgnoreCase) ||
                !Enum.IsDefined(journal.Stage) || journal.Files is null || journal.Files.Count > MaxJournalFiles ||
                journal.Failure is null || journal.Failure.Length > MaxFailureCharacters)
                return false;
            if (journal.SchemaVersion == 1) return ValidateLegacyTerminal(directory, installRoot, journal, out reason);
            if (journal.SchemaVersion != 2 || journal.InstallIdentity != DirectoryIdentity(installRoot) ||
                journal.TransactionIdentity != DirectoryIdentity(directory))
            {
                reason = "JOURNAL-IDENTITY";
                return false;
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in journal.Files)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.Path) ||
                    !SignedPackage.SafeRelativePath(entry.Path) || !seen.Add(entry.Path) ||
                    !IsSha256(entry.NewSha256) ||
                    (entry.Existed && (!IsSha256(entry.OriginalSha256) || entry.OriginalLength < 0 ||
                        !IsIdentity(entry.OriginalIdentity) || !IsIdentity(entry.BackupIdentity))) ||
                    entry.InstalledIdentity is null ||
                    (entry.InstalledIdentity.Length != 0 && !IsIdentity(entry.InstalledIdentity)))
                    return false;
                if (entry.TemporaryPath is null || (entry.TemporaryPath.Length != 0 &&
                    (entry.Existed || !SignedPackage.SafeRelativePath("payload/" + entry.TemporaryPath.Replace('\\', '/')) ||
                     !Path.GetFileName(entry.TemporaryPath).StartsWith(".pccare-" + journal.TransactionId + "-", StringComparison.Ordinal) ||
                     !entry.TemporaryPath.EndsWith(".pending", StringComparison.Ordinal) ||
                     Path.GetDirectoryName(entry.TemporaryPath) != Path.GetDirectoryName(Relative(entry.Path))))) return false;
            }
            var backup = Path.Combine(directory, "backup");
            if (Directory.Exists(backup))
            {
                EnsureRoot(backup);
                foreach (var entry in journal.Files.Where(x => x.Existed))
                    GuardExistingPath(backup, Relative(entry.Path));
            }
            reason = "OK";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            reason = "JOURNAL-INVALID";
            return false;
        }
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(c => Uri.IsHexDigit(c));

    private bool ValidateLegacyTerminal(string directory, string installRoot, UpdateJournal journal, out string reason)
    {
        reason = "LEGACY-RECOVERY-REQUIRED";
        if (journal.Stage is not (UpdateStage.Committed or UpdateStage.Restored)) return false;
        if (journal.InstallIdentity is null || journal.TransactionIdentity is null ||
            journal.InstallIdentity.Length != 0 || journal.TransactionIdentity.Length != 0 || journal.Files.Count == 0)
            return false;
        if (_strictAcl) VerifyProtectedAcl(directory);
        var receiptPath = Path.Combine(directory, "legacy-terminal.receipt");
        if (File.Exists(receiptPath))
        {
            using var receipt = WindowsFileBoundary.OpenRead(directory, "legacy-terminal.receipt");
            VerifyProtectedFile(receipt, directory);
            if (receipt.Stream.Length > 256) return false;
            using var reader = new StreamReader(receipt.Stream);
            if (reader.ReadToEnd() != LegacyFingerprint(directory, installRoot)) return false;
            reason = "LEGACY-ARCHIVED-READONLY";
            return true;
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in journal.Files)
        {
            if (entry is null || entry.OriginalIdentity is null || entry.BackupIdentity is null || entry.InstalledIdentity is null ||
                string.IsNullOrWhiteSpace(entry.Path) || !SignedPackage.SafeRelativePath(entry.Path) || !seen.Add(entry.Path) || !IsSha256(entry.NewSha256) ||
                entry.InstalledIdentity.Length != 0 || entry.OriginalIdentity.Length != 0 || entry.BackupIdentity.Length != 0)
                return false;
            var relative = Relative(entry.Path);
            if (entry.Existed)
            {
                if (!IsSha256(entry.OriginalSha256) || entry.OriginalLength < 0) return false;
                using var backup = WindowsFileBoundary.OpenRead(Path.Combine(directory, "backup"), relative);
                VerifyProtectedFile(backup, Path.Combine(directory, "backup"));
                backup.Verify(entry.OriginalLength, entry.OriginalSha256);
            }
            if (journal.Stage == UpdateStage.Restored && !entry.Existed)
            { if (File.Exists(Resolve(installRoot, relative))) return false; }
            else
            {
                using var target = WindowsFileBoundary.OpenRead(installRoot, relative);
                if (target.Hash() != (journal.Stage == UpdateStage.Committed ? entry.NewSha256 : entry.OriginalSha256)) return false;
            }
        }
        reason = "LEGACY-TERMINAL-READONLY";
        return true;
    }

    private string LegacyFingerprint(string directory, string installRoot)
    {
        using var original = WindowsFileBoundary.OpenRead(directory, "journal.json");
        VerifyProtectedFile(original, directory);
        return original.Hash() + "|" + DirectoryIdentity(installRoot) + "|" + DirectoryIdentity(directory);
    }

    private void ArchiveLegacyTerminal(string directory, string installRoot)
    {
        if (File.Exists(Path.Combine(directory, "legacy-terminal.receipt"))) return;
        using var parent = WindowsFileBoundary.PinDirectory(directory);
        var name = "legacy-" + Guid.NewGuid().ToString("N") + ".new";
        using (var receipt = WindowsFileBoundary.OpenWrite(directory, name, true))
        {
            VerifyProtectedFile(receipt, directory);
            receipt.Stream.Write(System.Text.Encoding.UTF8.GetBytes(LegacyFingerprint(directory, installRoot)));
            receipt.Stream.Flush(true);
        }
        parent.Validate();
        File.Move(Path.Combine(directory, name), Path.Combine(directory, "legacy-terminal.receipt"));
    }

    private static bool IsIdentity(string? value) => value is { Length: 49 } && value[16] == ':' &&
        value.Where((_, index) => index != 16).All(Uri.IsHexDigit);

    private static string DirectoryIdentity(string directory)
    {
        using var pin = WindowsFileBoundary.PinDirectory(directory);
        return pin.Identity;
    }

    private static TransactionResult FailureResult(UpdateJournal journal, Exception primary, Exception secondary) =>
        new(UpdateStage.RecoveryRequired, "FAILURE-JOURNAL-FAIL", journal.TransactionId, journal.Files.Count(f => f.Applied))
        { Failure = journal.Failure + "; primary: " + primary.Message + "; journal: " + secondary.Message };

    private TransactionResult RollbackCore(string txDir, string installRoot, UpdateJournal journal,
        Action<UpdateFaultPoint>? faultInjector = null)
    {
        var leases = new List<(UpdateFileJournal Entry, WindowsFileBoundary.FileLease? Backup, WindowsFileBoundary.FileLease? Target)>();
        try
        {
            // Keep every validated handle open until all restore operations end.
            foreach (var entry in journal.Files)
            {
                var relative = Relative(entry.Path);
                WindowsFileBoundary.FileLease? backup = null;
                WindowsFileBoundary.FileLease? target = null;
                try
                {
                    if (entry.Existed)
                    {
                        backup = WindowsFileBoundary.OpenRead(Path.Combine(txDir, "backup"), relative);
                        VerifyProtectedFile(backup, Path.GetDirectoryName(Resolve(Path.Combine(txDir, "backup"), relative))!);
                        backup.Verify(entry.OriginalLength, entry.OriginalSha256, entry.BackupIdentity);
                        target = WindowsFileBoundary.OpenWrite(installRoot, relative);
                        target.Validate(entry.OriginalIdentity);
                        var hash = target.Hash();
                        if (entry.Applied && hash != entry.NewSha256 && hash != entry.OriginalSha256)
                            throw new InvalidDataException("RESTORE-TARGET-CONTENT-CHANGED");
                    }
                    else if (File.Exists(Resolve(installRoot, relative)))
                    {
                        if (!IsIdentity(entry.InstalledIdentity)) throw new InvalidDataException("DELETE-IDENTITY-MISSING");
                        target = WindowsFileBoundary.OpenWrite(installRoot, relative, deletable: true);
                        target.Validate(entry.InstalledIdentity);
                        if ((entry.Applied || entry.TemporaryReady) && target.Hash() != entry.NewSha256)
                            throw new InvalidDataException("DELETE-TARGET-CONTENT-CHANGED");
                    }
                    else if (entry.TemporaryPath.Length != 0 && File.Exists(Resolve(installRoot, entry.TemporaryPath)))
                    {
                        if (!IsIdentity(entry.InstalledIdentity)) throw new InvalidDataException("TEMPORARY-IDENTITY-MISSING");
                        target = WindowsFileBoundary.OpenWrite(installRoot, entry.TemporaryPath, deletable: true);
                        target.Validate(entry.InstalledIdentity);
                        if (entry.TemporaryReady && target.Hash() != entry.NewSha256)
                            throw new InvalidDataException("TEMPORARY-CONTENT-CHANGED");
                    }
                    leases.Add((entry, backup, target));
                }
                catch { backup?.Dispose(); target?.Dispose(); throw; }
            }
            journal.Stage = UpdateStage.RollingBack;
            WriteJournal(txDir, journal);
            faultInjector?.Invoke(UpdateFaultPoint.AfterRollbackStarted);
            foreach (var item in leases.AsEnumerable().Reverse())
            {
                if (item.Entry.Existed)
                {
                    item.Target!.CopyFrom(item.Backup!);
                    faultInjector?.Invoke(UpdateFaultPoint.DuringRollbackCopy);
                    item.Target.Verify(item.Entry.OriginalLength, item.Entry.OriginalSha256, item.Entry.OriginalIdentity);
                }
                else if (item.Target is not null)
                {
                    item.Target.DeleteOpenedFile();
                }
                item.Entry.Restored = true;
                WriteJournal(txDir, journal);
            }
            faultInjector?.Invoke(UpdateFaultPoint.BeforeRestoredJournal);
            journal.Stage = UpdateStage.Restored;
            WriteJournal(txDir, journal);
            return new(UpdateStage.Restored, "ROLLED-BACK", journal.TransactionId, journal.Files.Count(f => f.Applied))
                { Failure = journal.Failure };
        }
        catch (Exception ex)
        {
            journal.Stage = UpdateStage.RecoveryRequired;
            journal.Failure += "; rollback: " + ex.GetType().Name + ": " + ex.Message;
            try { faultInjector?.Invoke(UpdateFaultPoint.BeforeFailureJournal); WriteJournal(txDir, journal); }
            catch (Exception journalError) { return FailureResult(journal, ex, journalError); }
            return new(UpdateStage.RecoveryRequired, "ROLLBACK-FAIL", journal.TransactionId, journal.Files.Count(f => f.Applied))
                { Failure = journal.Failure };
        }
        finally
        {
            foreach (var item in leases) { item.Target?.Dispose(); item.Backup?.Dispose(); }
        }
    }

    private void SecureWorkRoot()
    {
        var common = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        var productRoot = Path.Combine(common, "PC-Care");
        if (!string.Equals(_workRoot, Path.Combine(productRoot, "UpdateTransactions"),
                StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("unexpected protected staging location");
        var productExisted = Directory.Exists(productRoot);
        using var productPin = WindowsFileBoundary.PinDirectory(productRoot, create: true);
        if (!productExisted)
        {
            if (Directory.EnumerateFileSystemEntries(productRoot).Any())
                throw new UnauthorizedAccessException("staging parent raced");
            SetProtectedAcl(productRoot);
        }
        EnsureRoot(productRoot);
        VerifyProtectedAcl(productRoot);
        AllowReadOnlyInspection(productRoot);
        var workExisted = Directory.Exists(_workRoot);
        using var workPin = WindowsFileBoundary.PinDirectory(_workRoot, create: true);
        if (!workExisted)
        {
            if (Directory.EnumerateFileSystemEntries(_workRoot).Any())
                throw new UnauthorizedAccessException("staging root raced");
            SetProtectedAcl(_workRoot);
        }
        EnsureRoot(_workRoot);
        VerifyProtectedAcl(_workRoot);
        AllowReadOnlyInspection(_workRoot);
    }

    private static void VerifyProtectedAcl(string directory)
    {
        var security = new DirectoryInfo(directory).GetAccessControl();
        ValidateProtectedSecurity(security, requireProtected: true);
    }

    public static void ValidateProtectedSecurity(FileSystemSecurity security, bool requireProtected)
    {
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        if ((requireProtected && !security.AreAccessRulesProtected) || owner is null ||
            (!owner.Equals(admins) && !owner.Equals(system)))
            throw new UnauthorizedAccessException("untrusted staging ACL or owner");
        var adminControl = false;
        var systemControl = false;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Deny)
                throw new UnauthorizedAccessException("unexpected staging deny ACE");
            if ((rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
            {
                if (rule.IdentityReference.Equals(admins)) adminControl = true;
                if (rule.IdentityReference.Equals(system)) systemControl = true;
            }
            if (rule.AccessControlType == AccessControlType.Allow &&
                !rule.IdentityReference.Equals(admins) && !rule.IdentityReference.Equals(system) &&
                (rule.FileSystemRights & (FileSystemRights.WriteData | FileSystemRights.AppendData |
                                          FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes |
                                          FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions |
                                          FileSystemRights.TakeOwnership)) != 0)
                throw new UnauthorizedAccessException("staging writable by non-admin identity");
        }
        if (!adminControl || !systemControl) throw new UnauthorizedAccessException("staging administrative access missing");
    }

    public static void ValidateProtectedDirectory(string directory)
    {
        using var pin = WindowsFileBoundary.PinDirectory(directory);
        VerifyProtectedAcl(directory);
    }

    private static void SetProtectedAcl(string directory)
    {
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(admins);
        foreach (var sid in new[] { admins, system })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
    }

    private static void AllowReadOnlyInspection(string directory)
    {
        // Called only by the elevated strict-ACL path, after owner/DACL validation.
        // Existing WO03 roots remain fail-closed until the user approves this helper.
        var security = new DirectoryInfo(directory).GetAccessControl();
        security.SetAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
        VerifyProtectedAcl(directory);
    }

    private static string Relative(string path)
    {
        if (!SignedPackage.SafeRelativePath(path)) throw new InvalidDataException("unsafe path");
        return path["payload/".Length..].Replace('/', Path.DirectorySeparatorChar);
    }

    private static string Resolve(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("path traversal");
        return full;
    }

    private static void EnsureRoot(string root)
    {
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("untrusted root");
    }

    private static void GuardExistingPath(string root, string relative)
    {
        var path = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            path = Path.Combine(path, part);
            if ((File.Exists(path) || Directory.Exists(path)) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("reparse point");
        }
    }

    private static void VerifyFile(string path, long length, string hash)
    {
        using var file = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        file.Verify(length, hash);
    }

    private UpdateJournal? ReadJournal(string directory)
    {
        try { return ReadJournalFile(directory, "journal.json"); }
        catch (Exception ex) when (ex is JsonException ||
            (ex is IOException && !File.Exists(Path.Combine(directory, "journal.json"))))
        {
            // Recover only parse loss or an absent primary. Semantic rejection never selects an older record.
            return ReadJournalFile(directory, "journal.previous.json");
        }
    }

    private UpdateJournal? ReadJournalFile(string directory, string name)
    {
        using var file = WindowsFileBoundary.OpenRead(directory, name);
        VerifyProtectedFile(file, directory);
        if (file.Stream.Length > MaxJournalBytes) throw new InvalidDataException("journal exceeds size limit");
        using var document = JsonDocument.Parse(file.Stream);
        ValidateJsonProperties(document.RootElement);
        var known = typeof(UpdateJournal).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (document.RootElement.EnumerateObject().Any(p => !known.Contains(p.Name)))
            throw new InvalidDataException("unknown journal property");
        UpdateJournal? journal;
        try { journal = document.RootElement.Deserialize<UpdateJournal>(new JsonSerializerOptions
            { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }); }
        catch (JsonException ex) { throw new InvalidDataException("journal schema invalid", ex); }
        if (journal is not null && !document.RootElement.TryGetProperty("SchemaVersion", out _)) journal.SchemaVersion = 1;
        return journal;
    }

    private static void ValidateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("duplicate journal property");
                ValidateJsonProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) ValidateJsonProperties(item);
    }

    private void WriteJournal(string directory, UpdateJournal journal)
    {
        using var parent = WindowsFileBoundary.PinDirectory(directory);
        if (journal.TransactionIdentity != parent.Identity) throw new IOException("JOURNAL-DIRECTORY-IDENTITY");
        var path = Path.Combine(directory, "journal.json");
        var relative = "journal-" + Guid.NewGuid().ToString("N") + ".new";
        if (File.Exists(Path.Combine(directory, "journal.previous.json")))
        {
            using var previous = WindowsFileBoundary.OpenRead(directory, "journal.previous.json");
            VerifyProtectedFile(previous, directory);
            previous.Validate();
        }
        if (journal.Failure.Length > MaxFailureCharacters) journal.Failure = journal.Failure[..MaxFailureCharacters];
        var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
        if (journal.Files.Count > MaxJournalFiles || bytes.Length > MaxJournalBytes)
            throw new InvalidDataException("journal exceeds recovery limits");
        if (File.Exists(path))
        {
            using var existing = WindowsFileBoundary.OpenRead(directory, "journal.json");
            VerifyProtectedFile(existing, directory);
            existing.Validate();
        }
        using (var file = WindowsFileBoundary.OpenWrite(directory, relative, create: true))
        {
            VerifyProtectedFile(file, directory);
            _journalFault?.Invoke(UpdateFaultPoint.JournalTempCreated);
            var midpoint = bytes.Length / 2;
            file.Stream.Write(bytes.AsSpan(0, midpoint));
            _journalFault?.Invoke(UpdateFaultPoint.JournalPartialWrite);
            file.Stream.Write(bytes.AsSpan(midpoint));
            file.Stream.Flush(flushToDisk: true);
            _journalFault?.Invoke(UpdateFaultPoint.JournalFlushed);
            file.Validate();
        }
        parent.Validate();
        // The protected parent ACL excludes unprivileged journal replacement.
        // Administrators remain inside the trust boundary during this rename.
        _journalFault?.Invoke(UpdateFaultPoint.JournalBeforeReplace);
        if (File.Exists(path))
            File.Replace(Path.Combine(directory, relative), path, Path.Combine(directory, "journal.previous.json"));
        else File.Move(Path.Combine(directory, relative), path);
        _journalFault?.Invoke(UpdateFaultPoint.JournalAfterReplace);
    }

    private void VerifyProtectedFile(WindowsFileBoundary.FileLease file, string directory)
    {
        if (!_strictAcl) return;
        ValidateProtectedSecurity(file.ReadSecurity(), requireProtected: false);
        var current = Path.GetFullPath(directory);
        var root = _workRoot.TrimEnd(Path.DirectorySeparatorChar);
        while (current.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            using var pin = WindowsFileBoundary.PinDirectory(current);
            ValidateProtectedSecurity(new DirectoryInfo(current).GetAccessControl(), requireProtected: false);
            current = Path.GetDirectoryName(current)!;
        }
        if (!string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("protected file outside staging root");
        VerifyProtectedAcl(root);
    }
    private static bool FitsRecoveryJournal(SignedManifest manifest, string installRoot, string currentVersion)
    {
        if (manifest.Files.Count > MaxJournalFiles) return false;
        // Reserve the largest backup metadata and escaped failure text before changing installation files.
        var projected = new UpdateJournal
        {
            TransactionId = new string('f', 32), InstallRoot = installRoot,
            InstallIdentity = new string('F', 49), TransactionIdentity = new string('F', 49),
            PackageId = manifest.PackageId, FromVersion = currentVersion, ToVersion = manifest.ToVersion,
            Stage = UpdateStage.RecoveryRequired, Failure = new string('\u0001', MaxFailureCharacters),
            Files = manifest.Files.Select(file => new UpdateFileJournal
            {
                Path = file.Path, Existed = true, OriginalLength = long.MaxValue,
                OriginalSha256 = new string('F', 64), NewSha256 = file.Sha256,
                OriginalIdentity = new string('F', 49), BackupIdentity = new string('F', 49), InstalledIdentity = new string('F', 49),
                TemporaryPath = Path.Combine(Path.GetDirectoryName(Relative(file.Path)) ?? "", ".pccare-" + new string('f', 32) + "-" + new string('f', 32) + ".pending"),
                Applied = false, Restored = false
            }).ToList()
        };
        return JsonSerializer.SerializeToUtf8Bytes(projected, JsonOptions).Length <= MaxJournalBytes;
    }
}
