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
    public string Path { get; set; } = "";
    public bool Existed { get; set; }
    public long OriginalLength { get; set; }
    public string OriginalSha256 { get; set; } = "";
    public string NewSha256 { get; set; } = "";
    public bool Applied { get; set; }
    public bool Restored { get; set; }
}

public sealed record TransactionResult(UpdateStage Stage, string Code, string TransactionId, int FilesApplied);

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
    DuringRollbackCopy, BeforeRestoredJournal
}

public sealed record UpdateRecoveryStatus(UpdateRecoveryState State, string Code, string TransactionId);

public sealed class UpdateTransaction
{
    private readonly PublisherTrustPolicy _policy;
    private readonly string _workRoot;
    private readonly bool _strictAcl;
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

    public TransactionResult Apply(string packagePath, string installRoot, string currentVersion,
        Action<int>? afterFileApplied = null, Func<string, bool>? postApplyVersionCheck = null,
        Action<UpdateFaultPoint>? faultInjector = null)
    {
        installRoot = Path.GetFullPath(installRoot);
        EnsureRoot(installRoot);
        if (_strictAcl) SecureWorkRoot();
        else Directory.CreateDirectory(_workRoot);
        EnsureRoot(_workRoot);
        using var gate = new FileStream(Path.Combine(_workRoot, "update.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        var priorJournals = new List<UpdateJournal>();
        foreach (var dir in Directory.EnumerateDirectories(_workRoot, "tx-*"))
        {
            if (!TryReadTrustedJournal(dir, installRoot, out var prior, out var reason) || prior is null)
                return new(UpdateStage.RecoveryRequired, reason, "", 0);
            if (_strictAcl) { VerifyProtectedAcl(dir); AllowReadOnlyInspection(dir); }
            if (prior.Stage is not (UpdateStage.Committed or UpdateStage.Restored))
                return new(UpdateStage.RecoveryRequired, "PRIOR-TRANSACTION", prior.TransactionId, 0);
            priorJournals.Add(prior);
        }

        var source = SignedPackage.Verify(packagePath, currentVersion, _policy);
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
            InstallRoot = installRoot, FromVersion = currentVersion, Stage = UpdateStage.Prepared
        };
        WriteJournal(txDir, journal);
        var packageCopy = Path.Combine(txDir, "package.spdup");
        var stageDir = Path.Combine(txDir, "stage");
        var backupDir = Path.Combine(txDir, "backup");
        try
        {
            faultInjector?.Invoke(UpdateFaultPoint.AfterStagingCreated);
            File.Copy(packagePath, packageCopy, overwrite: false);
            var stagedVerification = SignedPackage.Verify(packageCopy, currentVersion, _policy);
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
            using (var package = new FileStream(packageCopy, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(package, ZipArchiveMode.Read))
            {
                foreach (var item in manifest.Files)
                {
                    var relative = Relative(item.Path);
                    var stageFile = Resolve(stageDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(stageFile)!);
                    using (var input = archive.GetEntry(item.Path)!.Open())
                    using (var output = new FileStream(stageFile, FileMode.CreateNew, FileAccess.Write,
                               FileShare.None, 128 * 1024, FileOptions.WriteThrough))
                    {
                        input.CopyTo(output);
                        output.Flush(flushToDisk: true);
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
                    entry.OriginalLength = new FileInfo(target).Length;
                    entry.OriginalSha256 = Hash(target);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, overwrite: false);
                    faultInjector?.Invoke(UpdateFaultPoint.DuringBackupCreation);
                    VerifyFile(backup, entry.OriginalLength, entry.OriginalSha256);
                }
                journal.Files.Add(entry);
            }
            journal.Stage = UpdateStage.BackupVerified;
            WriteJournal(txDir, journal);
            faultInjector?.Invoke(UpdateFaultPoint.AfterBackupVerification);
            journal.Stage = UpdateStage.Applying;
            WriteJournal(txDir, journal); // durable before the first file mutation
            faultInjector?.Invoke(UpdateFaultPoint.AfterApplyingJournal);
            var count = 0;
            foreach (var entry in journal.Files)
            {
                var relative = Relative(entry.Path);
                GuardExistingPath(installRoot, relative);
                var sourceFile = Resolve(stageDir, relative);
                var target = Resolve(installRoot, relative);
                var expected = manifest.Files.Single(f => f.Path == entry.Path);
                VerifyFile(sourceFile, expected.Length, expected.Sha256); // immediately before privileged read
                faultInjector?.Invoke(UpdateFaultPoint.BeforeReplacement);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temp = target + ".pccare-" + id + ".tmp";
                using (var input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 128 * 1024, FileOptions.WriteThrough))
                {
                    input.CopyTo(output);
                    output.Flush(flushToDisk: true);
                }
                VerifyFile(temp, expected.Length, expected.Sha256);
                File.Move(temp, target, overwrite: true);
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
                VerifyFile(Resolve(installRoot, Relative(item.Path)), item.Length, item.Sha256);
            }
            if (postApplyVersionCheck is not null && !postApplyVersionCheck(manifest.ToVersion))
                throw new InvalidDataException("installed version postcondition");
            faultInjector?.Invoke(UpdateFaultPoint.BeforeCommittedJournal);
            journal.Stage = UpdateStage.Committed;
            WriteJournal(txDir, journal);
            try { File.Delete(packageCopy); } catch (Exception) { /* retain evidence */ }
            return new(UpdateStage.Committed, "COMMITTED", id, count);
        }
        catch (Exception ex)
        {
            journal.Failure = ex.GetType().Name + ": " + ex.Message;
            journal.Stage = UpdateStage.RollbackPending;
            WriteJournal(txDir, journal);
            return RollbackCore(txDir, installRoot, journal, faultInjector);
        }
    }

    public TransactionResult Recover(string installRoot, Action<UpdateFaultPoint>? faultInjector = null)
    {
        installRoot = Path.GetFullPath(installRoot);
        EnsureRoot(installRoot);
        if (_strictAcl) SecureWorkRoot();
        else Directory.CreateDirectory(_workRoot);
        EnsureRoot(_workRoot);
        using var gate = new FileStream(Path.Combine(_workRoot, "update.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        foreach (var dir in Directory.EnumerateDirectories(_workRoot, "tx-*").OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!TryReadTrustedJournal(dir, installRoot, out var journal, out var reason))
                return new(UpdateStage.RecoveryRequired, reason, "", 0);
            if (_strictAcl) { VerifyProtectedAcl(dir); AllowReadOnlyInspection(dir); }
            if (journal is null) return new(UpdateStage.RecoveryRequired, "JOURNAL-MISSING", "", 0);
            if (journal.Stage is UpdateStage.Committed or UpdateStage.Restored) continue;
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

    private static bool TryReadTrustedJournal(string directory, string installRoot,
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
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
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
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in journal.Files)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.Path) ||
                    !SignedPackage.SafeRelativePath(entry.Path) || !seen.Add(entry.Path) ||
                    !IsSha256(entry.NewSha256) ||
                    (entry.Existed && (!IsSha256(entry.OriginalSha256) || entry.OriginalLength < 0)))
                    return false;
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

    private static TransactionResult RollbackCore(string txDir, string installRoot, UpdateJournal journal,
        Action<UpdateFaultPoint>? faultInjector = null)
    {
        try
        {
            // Validate every backup before any restore/delete so a damaged backup cannot
            // cause a partially restored installation to be changed further.
            foreach (var entry in journal.Files)
            {
                var relative = Relative(entry.Path);
                GuardExistingPath(installRoot, relative);
                if (entry.Existed)
                {
                    var backupRoot = Path.Combine(txDir, "backup");
                    GuardExistingPath(backupRoot, relative);
                    VerifyFile(Resolve(backupRoot, relative), entry.OriginalLength, entry.OriginalSha256);
                }
                else
                {
                    var target = Resolve(installRoot, relative);
                    if (File.Exists(target)) VerifyFile(target, new FileInfo(target).Length, entry.NewSha256);
                }
            }
            journal.Stage = UpdateStage.RollingBack;
            WriteJournal(txDir, journal);
            faultInjector?.Invoke(UpdateFaultPoint.AfterRollbackStarted);
            foreach (var entry in journal.Files.AsEnumerable().Reverse())
            {
                var relative = Relative(entry.Path);
                GuardExistingPath(installRoot, relative);
                var target = Resolve(installRoot, relative);
                if (entry.Existed)
                {
                    var backup = Resolve(Path.Combine(txDir, "backup"), relative);
                    VerifyFile(backup, entry.OriginalLength, entry.OriginalSha256);
                    var temp = target + ".pccare-restore-" + Guid.NewGuid().ToString("N") + ".tmp";
                    File.Copy(backup, temp, overwrite: false);
                    faultInjector?.Invoke(UpdateFaultPoint.DuringRollbackCopy);
                    VerifyFile(temp, entry.OriginalLength, entry.OriginalSha256);
                    File.Move(temp, target, overwrite: true);
                }
                else if (File.Exists(target))
                {
                    // A newly created file may be removed only while it still has the expected update hash.
                    VerifyFile(target, new FileInfo(target).Length, entry.NewSha256);
                    File.Delete(target);
                }
                entry.Restored = true;
                WriteJournal(txDir, journal);
            }
            faultInjector?.Invoke(UpdateFaultPoint.BeforeRestoredJournal);
            journal.Stage = UpdateStage.Restored;
            WriteJournal(txDir, journal);
            return new(UpdateStage.Restored, "ROLLED-BACK", journal.TransactionId, journal.Files.Count(f => f.Applied));
        }
        catch (Exception ex)
        {
            journal.Stage = UpdateStage.RecoveryRequired;
            journal.Failure += "; rollback: " + ex.GetType().Name + ": " + ex.Message;
            WriteJournal(txDir, journal);
            return new(UpdateStage.RecoveryRequired, "ROLLBACK-FAIL", journal.TransactionId, journal.Files.Count(f => f.Applied));
        }
    }

    private void SecureWorkRoot()
    {
        var common = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        var productRoot = Path.Combine(common, "PC-Care");
        if (!string.Equals(_workRoot, Path.Combine(productRoot, "UpdateTransactions"),
                StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("unexpected protected staging location");
        if (!Directory.Exists(productRoot))
        {
            Directory.CreateDirectory(productRoot);
            if (Directory.EnumerateFileSystemEntries(productRoot).Any())
                throw new UnauthorizedAccessException("staging parent raced");
            SetProtectedAcl(productRoot);
        }
        EnsureRoot(productRoot);
        VerifyProtectedAcl(productRoot);
        AllowReadOnlyInspection(productRoot);
        if (!Directory.Exists(_workRoot))
        {
            Directory.CreateDirectory(_workRoot);
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
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        if (!security.AreAccessRulesProtected || owner is null ||
            (!owner.Equals(admins) && !owner.Equals(system)))
            throw new UnauthorizedAccessException("untrusted staging ACL or owner");
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                !rule.IdentityReference.Equals(admins) && !rule.IdentityReference.Equals(system) &&
                (rule.FileSystemRights & (FileSystemRights.WriteData | FileSystemRights.AppendData |
                                          FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes |
                                          FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions |
                                          FileSystemRights.TakeOwnership)) != 0)
                throw new UnauthorizedAccessException("staging writable by non-admin identity");
        }
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

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void VerifyFile(string path, long length, string hash)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != length ||
            !Hash(path).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("file integrity: " + path);
    }

    private static UpdateJournal? ReadJournal(string directory)
    {
        var path = Path.Combine(directory, "journal.json");
        if (File.Exists(path) && new FileInfo(path).Length > MaxJournalBytes)
            throw new InvalidDataException("journal exceeds size limit");
        return File.Exists(path) ? JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path)) : null;
    }

    private static void WriteJournal(string directory, UpdateJournal journal)
    {
        var path = Path.Combine(directory, "journal.json");
        var temp = path + ".new";
        if (journal.Failure.Length > MaxFailureCharacters)
            journal.Failure = journal.Failure[..MaxFailureCharacters];
        var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
        if (journal.Files.Count > MaxJournalFiles || bytes.Length > MaxJournalBytes)
            throw new InvalidDataException("journal exceeds recovery limits");
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write,
                   FileShare.None, 4096, FileOptions.WriteThrough))
        {
            file.Write(bytes);
            file.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }

    private static bool FitsRecoveryJournal(SignedManifest manifest, string installRoot, string currentVersion)
    {
        if (manifest.Files.Count > MaxJournalFiles) return false;
        // Reserve the largest backup metadata and escaped failure text before changing installation files.
        var projected = new UpdateJournal
        {
            TransactionId = new string('f', 32), InstallRoot = installRoot,
            PackageId = manifest.PackageId, FromVersion = currentVersion, ToVersion = manifest.ToVersion,
            Stage = UpdateStage.RecoveryRequired, Failure = new string('\u0001', MaxFailureCharacters),
            Files = manifest.Files.Select(file => new UpdateFileJournal
            {
                Path = file.Path, Existed = true, OriginalLength = long.MaxValue,
                OriginalSha256 = new string('F', 64), NewSha256 = file.Sha256,
                Applied = false, Restored = false
            }).ToList()
        };
        return JsonSerializer.SerializeToUtf8Bytes(projected, JsonOptions).Length <= MaxJournalBytes;
    }
}
