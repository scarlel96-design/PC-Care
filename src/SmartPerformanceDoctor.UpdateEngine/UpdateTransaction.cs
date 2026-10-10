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

public sealed class UpdateTransaction
{
    private readonly PublisherTrustPolicy _policy;
    private readonly string _workRoot;
    private readonly bool _strictAcl;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UpdateTransaction(PublisherTrustPolicy policy, string workRoot, bool strictAcl)
    {
        _policy = policy;
        _workRoot = Path.GetFullPath(workRoot);
        _strictAcl = strictAcl;
    }

    public TransactionResult Apply(string packagePath, string installRoot, string currentVersion,
        Action<int>? afterFileApplied = null, Func<string, bool>? postApplyVersionCheck = null)
    {
        installRoot = Path.GetFullPath(installRoot);
        EnsureRoot(installRoot);
        if (_strictAcl) SecureWorkRoot();
        else Directory.CreateDirectory(_workRoot);
        EnsureRoot(_workRoot);
        using var gate = new FileStream(Path.Combine(_workRoot, "update.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        if (Directory.EnumerateDirectories(_workRoot, "tx-*").Any(dir =>
            ReadJournal(dir)?.Stage is not (UpdateStage.Committed or UpdateStage.Restored)))
            return new(UpdateStage.RecoveryRequired, "PRIOR-TRANSACTION", "", 0);

        var source = SignedPackage.Verify(packagePath, currentVersion, _policy);
        if (!source.CanApply || source.Manifest is null)
            return new(UpdateStage.Prepared, source.Code, "", 0);
        if (Directory.EnumerateDirectories(_workRoot, "tx-*").Any(dir =>
            ReadJournal(dir) is { Stage: UpdateStage.Committed } prior &&
            string.Equals(prior.PackageId, source.Manifest.PackageId, StringComparison.OrdinalIgnoreCase)))
            return new(UpdateStage.Prepared, "REPLAY", "", 0);

        var id = Guid.NewGuid().ToString("N");
        var txDir = Path.Combine(_workRoot, "tx-" + id);
        Directory.CreateDirectory(txDir);
        if (_strictAcl) SetProtectedAcl(txDir);
        var journal = new UpdateJournal
        {
            TransactionId = id, PackageId = source.Manifest.PackageId,
            FromVersion = currentVersion, Stage = UpdateStage.Prepared
        };
        WriteJournal(txDir, journal);
        var packageCopy = Path.Combine(txDir, "package.spdup");
        var stageDir = Path.Combine(txDir, "stage");
        var backupDir = Path.Combine(txDir, "backup");
        try
        {
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
                    VerifyFile(backup, entry.OriginalLength, entry.OriginalSha256);
                }
                journal.Files.Add(entry);
            }
            journal.Stage = UpdateStage.BackupVerified;
            WriteJournal(txDir, journal);
            journal.Stage = UpdateStage.Applying;
            WriteJournal(txDir, journal); // durable before the first file mutation
            var count = 0;
            foreach (var entry in journal.Files)
            {
                var relative = Relative(entry.Path);
                GuardExistingPath(installRoot, relative);
                var sourceFile = Resolve(stageDir, relative);
                var target = Resolve(installRoot, relative);
                var expected = manifest.Files.Single(f => f.Path == entry.Path);
                VerifyFile(sourceFile, expected.Length, expected.Sha256); // immediately before privileged read
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
                afterFileApplied?.Invoke(++count);
            }
            journal.Stage = UpdateStage.PostApplyVerification;
            WriteJournal(txDir, journal);
            foreach (var item in manifest.Files)
                VerifyFile(Resolve(installRoot, Relative(item.Path)), item.Length, item.Sha256);
            if (postApplyVersionCheck is not null && !postApplyVersionCheck(manifest.ToVersion))
                throw new InvalidDataException("installed version postcondition");
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
            return RollbackCore(txDir, installRoot, journal);
        }
    }

    public TransactionResult Recover(string installRoot)
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
            var journal = ReadJournal(dir);
            if (journal is null) return new(UpdateStage.RecoveryRequired, "JOURNAL-MISSING", "", 0);
            if (journal.Stage is UpdateStage.Committed or UpdateStage.Restored) continue;
            if (journal.Stage < UpdateStage.Applying)
            {
                journal.Stage = UpdateStage.Restored;
                WriteJournal(dir, journal);
                continue;
            }
            return RollbackCore(dir, installRoot, journal);
        }
        return new(UpdateStage.Restored, "NO-PENDING", "", 0);
    }

    private static TransactionResult RollbackCore(string txDir, string installRoot, UpdateJournal journal)
    {
        try
        {
            journal.Stage = UpdateStage.RollingBack;
            WriteJournal(txDir, journal);
            foreach (var entry in journal.Files.AsEnumerable().Reverse())
            {
                var relative = Relative(entry.Path);
                GuardExistingPath(installRoot, relative);
                var target = Resolve(installRoot, relative);
                if (entry.Existed)
                {
                    var backup = Resolve(Path.Combine(txDir, "backup"), relative);
                    VerifyFile(backup, entry.OriginalLength, entry.OriginalSha256);
                    var temp = target + ".pccare-restore-" + journal.TransactionId + ".tmp";
                    File.Copy(backup, temp, overwrite: false);
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
        if (!Directory.Exists(_workRoot))
        {
            Directory.CreateDirectory(_workRoot);
            if (Directory.EnumerateFileSystemEntries(_workRoot).Any())
                throw new UnauthorizedAccessException("staging root raced");
            SetProtectedAcl(_workRoot);
        }
        EnsureRoot(_workRoot);
        VerifyProtectedAcl(_workRoot);
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
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                !rule.IdentityReference.Equals(admins) && !rule.IdentityReference.Equals(system) &&
                (rule.FileSystemRights & (FileSystemRights.Write | FileSystemRights.Modify |
                                          FileSystemRights.Delete | FileSystemRights.ChangePermissions |
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
        new DirectoryInfo(directory).SetAccessControl(security);
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
        return File.Exists(path) ? JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path)) : null;
    }

    private static void WriteJournal(string directory, UpdateJournal journal)
    {
        var path = Path.Combine(directory, "journal.json");
        var temp = path + ".new";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write,
                   FileShare.None, 4096, FileOptions.WriteThrough))
        {
            file.Write(bytes);
            file.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}
