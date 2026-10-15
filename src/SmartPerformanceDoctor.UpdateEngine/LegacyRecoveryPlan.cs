using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record LegacyRecoveryTarget(string Path, string CurrentIdentity, string CurrentHash,
    string BackupIdentity, string AuthenticatedOriginalHash, long OriginalLength, string Status)
{
    public long CurrentLength { get; init; } = -1;
    public string CurrentSecurity { get; init; } = "";
    public string CurrentContentState { get; init; } = "UnknownContent";
}
public sealed record LegacyRecoveryPlan(string TransactionId, string JournalHash, string InstallIdentity,
    string TransactionIdentity, string OriginalPackageHash, string OriginalVersion, string Status,
    bool CanExecute, string ConsentBinding, IReadOnlyList<LegacyRecoveryTarget> Targets)
{ public string PackageIdentity { get; init; } = ""; }

public sealed partial class UpdateTransaction
{
    /// <summary>Bind a read-only plan to independently signed original-version evidence.</summary>
    public LegacyRecoveryPlan PlanLegacyRecovery(string installRoot, string transactionId,
        string originalPackage, string packagePreviousVersion)
    {
        if (!Guid.TryParseExact(transactionId, "N", out _)) throw new InvalidDataException("LEGACY-ID");
        var diagnosis = DiagnoseLegacy(installRoot).SingleOrDefault(item => item.TransactionId == transactionId)
            ?? throw new InvalidDataException("LEGACY-NOT-FOUND");
        var targets = new List<LegacyRecoveryTarget>();
        var packageHash = ""; var packageIdentity = ""; var originalVersion = ""; var state = "RecoveryDenied";
        if (diagnosis.Code.StartsWith("READONLY-MANUAL-REVIEW", StringComparison.Ordinal))
        {
            var packageFull = Path.GetFullPath(originalPackage);
            using var package = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(packageFull)!, Path.GetFileName(packageFull));
            packageHash = package.Hash(); packageIdentity = package.Identity;
            var verification = SignedPackage.Verify(package.Stream, packagePreviousVersion, _policy);
            if (verification.CanApply && verification.Manifest is not null)
            {
                var txDir = Path.Combine(_workRoot, "tx-" + transactionId);
                using var record = WindowsFileBoundary.OpenRead(txDir, "journal.json");
                if (record.Hash() != diagnosis.JournalHash) throw new InvalidDataException("LEGACY-CHANGED");
                var journal = ReadJournalFile(txDir, "journal.json")!;
                originalVersion = verification.Manifest.ToVersion;
                if (originalVersion == journal.FromVersion)
                {
                    state = "ManualReviewRequired";
                    foreach (var observed in diagnosis.Files)
                    {
                        var expected = verification.Manifest.Files.SingleOrDefault(item => item.Path == observed.Path);
                        var entry = journal.Files.Single(item => item.Path == observed.Path);
                        var status = "RecoveryDenied";
                        if (entry.Existed && expected is not null)
                        {
                            using var backup = WindowsFileBoundary.OpenRead(Path.Combine(txDir, "backup"), Relative(observed.Path));
                            VerifyProtectedFile(backup, Path.Combine(txDir, "backup"));
                            backup.Verify(expected.Length, expected.Sha256, observed.BackupIdentity);
                            if (expected.Sha256 == entry.OriginalSha256 && expected.Length == entry.OriginalLength)
                                status = "BackupVerified";
                        }
                        using var current = WindowsFileBoundary.OpenRead(installRoot, Relative(observed.Path));
                        targets.Add(new(observed.Path, observed.InstalledIdentity, observed.InstalledHash,
                            observed.BackupIdentity, expected?.Sha256 ?? "", expected?.Length ?? -1, status)
                        { CurrentLength = current.Stream.Length, CurrentSecurity = current.ReadSecurity().GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All),
                            CurrentContentState = ClassifyLegacyContent(txDir, journal, observed, current.Stream.Length, expected) });
                    }
                }
                record.Validate(); package.Validate();
            }
        }
        // A read-only plan grants no mutation authority. Administrator execution needs its own reviewed contract.
        var binding = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { diagnosis.TransactionId, diagnosis.JournalHash, diagnosis.InstallIdentity, diagnosis.TransactionIdentity,
            PackageHash = packageHash, PackageIdentity = packageIdentity, OriginalVersion = originalVersion, Targets = targets })));
        return new(diagnosis.TransactionId, diagnosis.JournalHash, diagnosis.InstallIdentity,
            diagnosis.TransactionIdentity, packageHash, originalVersion, state, false, binding, targets) { PackageIdentity = packageIdentity };
    }
}


public enum LegacyRestoreFaultPoint { AuthorizationVerified, BeforePromotion, AfterPromotion, BeforeRestore, AfterFileRestore, BeforeRestoredJournal, AfterRestoredJournal }

public sealed partial class UpdateTransaction
{
    /// <summary>Restore only signed, approved existing files. Preserve the original legacy record.</summary>
    public TransactionResult ExecuteLegacyRecovery(string installRoot, LegacyRecoveryPlan approved,
        string originalPackage, string packagePreviousVersion, string approvedBinding, bool consent,
        Action<LegacyRestoreFaultPoint>? fault = null)
    {
        if (_strictAcl && _approvedLegacyBinding.Value != approvedBinding)
            return new(UpdateStage.RecoveryRequired, "LEGACY-ELEVATED-APPROVAL-REQUIRED", approved.TransactionId, 0);
        var bound = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { approved.TransactionId, approved.JournalHash, approved.InstallIdentity, approved.TransactionIdentity,
            PackageHash=approved.OriginalPackageHash, approved.PackageIdentity, approved.OriginalVersion, approved.Targets })));
        if (!consent || approvedBinding != approved.ConsentBinding || bound != approvedBinding)
            return new(UpdateStage.RecoveryRequired, "LEGACY-CONSENT-REQUIRED", approved.TransactionId, 0);
        if (_strictAcl && !new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            throw new UnauthorizedAccessException("LEGACY-ADMIN-REQUIRED");
        installRoot = Path.GetFullPath(installRoot);
        EnsureRoot(installRoot); EnsureRoot(_workRoot);
        if (_strictAcl) { VerifyProtectedAcl(_workRoot); ValidateProtectedSecurity(new DirectoryInfo(installRoot).GetAccessControl(), false); }
        using var install = WindowsFileBoundary.PinDirectory(installRoot);
        using var work = WindowsFileBoundary.PinDirectory(_workRoot);
        using var gate = WindowsFileBoundary.OpenExclusive(_workRoot, "update.lock", !File.Exists(Path.Combine(_workRoot,"update.lock")));
        VerifyProtectedFile(gate, _workRoot);
        var fresh = PlanLegacyRecovery(installRoot, approved.TransactionId, originalPackage, packagePreviousVersion);
        if (fresh.ConsentBinding != approvedBinding || fresh.Status != "ManualReviewRequired" || fresh.Targets.Count == 0 ||
            fresh.Targets.Any(t => t.Status != "BackupVerified" || t.CurrentIdentity.Length == 0 || t.CurrentLength < 0 ||
                t.CurrentContentState is not ("OriginalVerified" or "UpdatedVerified")))
            return new(UpdateStage.RecoveryRequired, "LEGACY-PLAN-CHANGED-OR-UNTRUSTED", approved.TransactionId, 0);
        var tx = Path.Combine(_workRoot,"tx-"+approved.TransactionId);
        foreach (var directory in Directory.EnumerateDirectories(_workRoot,"tx-*"))
        {
            if (directory == tx) continue;
            if (!TryReadTrustedJournal(directory,installRoot,out var other,out _) || other is null ||
                other.Stage is not (UpdateStage.Committed or UpdateStage.Restored))
                return new(UpdateStage.RecoveryRequired,"MULTIPLE-PENDING-TRANSACTIONS",approved.TransactionId,0);
        }
        using var transaction = WindowsFileBoundary.PinDirectory(tx);
        using var package = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(originalPackage))!,Path.GetFileName(originalPackage));
        if (package.Identity!=approved.PackageIdentity || package.Hash()!=fresh.OriginalPackageHash || !SignedPackage.Verify(package.Stream,packagePreviousVersion,_policy).CanApply)
            throw new InvalidDataException("LEGACY-PACKAGE-CHANGED");
        using (var original = WindowsFileBoundary.OpenRead(tx,"journal.json"))
        {
            if(original.Hash()!=approved.JournalHash) throw new InvalidDataException("LEGACY-JOURNAL-CHANGED");
            var retained="legacy-original-"+approved.JournalHash+".json";
            if (!File.Exists(Path.Combine(tx,retained)))
            { using var copy=WindowsFileBoundary.OpenWrite(tx,retained,true); VerifyProtectedFile(copy,tx); copy.CopyFrom(original); copy.Verify(original.Stream.Length,approved.JournalHash); }
            else { using var copy=WindowsFileBoundary.OpenRead(tx,retained); copy.Verify(original.Stream.Length,approved.JournalHash); }
        }
        fault?.Invoke(LegacyRestoreFaultPoint.AuthorizationVerified);
        using(var recheck=WindowsFileBoundary.OpenRead(tx,"journal.json"))
        { if(recheck.Hash()!=approved.JournalHash)throw new InvalidDataException("LEGACY-JOURNAL-CHANGED"); }
        var journal=ReadJournalFile(tx,"journal.json")!;
        if(journal.SchemaVersion!=1 || journal.Files.Any(f=>!f.Existed) || journal.Stage is UpdateStage.Committed or UpdateStage.Restored)
            throw new InvalidDataException("LEGACY-EXECUTION-SCOPE");
        var leases=new List<WindowsFileBoundary.FileLease>();
        try
        {
            foreach(var entry in journal.Files)
            {
                var expected=fresh.Targets.Single(t=>t.Path==entry.Path);
                var backup=WindowsFileBoundary.OpenRead(Path.Combine(tx,"backup"),Relative(entry.Path)); leases.Add(backup);
                VerifyProtectedFile(backup,Path.Combine(tx,"backup")); backup.Verify(expected.OriginalLength,expected.AuthenticatedOriginalHash,expected.BackupIdentity);
                var target=WindowsFileBoundary.OpenWrite(installRoot,Relative(entry.Path)); leases.Add(target);
                if(_strictAcl) ValidateProtectedSecurity(target.ReadSecurity(),false);
                target.Verify(expected.CurrentLength,expected.CurrentHash,expected.CurrentIdentity);
                if(target.ReadSecurity().GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All)!=expected.CurrentSecurity)
                    throw new InvalidDataException("LEGACY-TARGET-ACL-CHANGED");
                entry.OriginalIdentity=entry.InstalledIdentity=target.Identity; entry.BackupIdentity=backup.Identity;
                entry.NewSha256=expected.CurrentHash; entry.NewLength=expected.CurrentLength; entry.Applied=true; entry.Restored=false;
            }
            install.Validate();work.Validate();transaction.Validate();package.Validate();
            journal.SchemaVersion=2; journal.InstallIdentity=install.Identity; journal.TransactionIdentity=transaction.Identity;
            journal.Stage=UpdateStage.RollbackPending;
            fault?.Invoke(LegacyRestoreFaultPoint.BeforePromotion);
            WriteJournal(tx,journal);
        }
        finally { foreach(var lease in leases) lease.Dispose(); }
        fault?.Invoke(LegacyRestoreFaultPoint.AfterPromotion);
        fault?.Invoke(LegacyRestoreFaultPoint.BeforeRestore);
        var result=RollbackCore(tx,installRoot,journal,p=>
        {
            if(p==UpdateFaultPoint.DuringRollbackCopy) fault?.Invoke(LegacyRestoreFaultPoint.AfterFileRestore);
            if(p==UpdateFaultPoint.BeforeRestoredJournal) fault?.Invoke(LegacyRestoreFaultPoint.BeforeRestoredJournal);
        });
        if(result.Stage==UpdateStage.Restored) fault?.Invoke(LegacyRestoreFaultPoint.AfterRestoredJournal);
        return result;
    }
}

public sealed record LegacyRestoreRequest(LegacyRecoveryPlan Plan,string OriginalPackage,string PackagePreviousVersion)
{ public LegacyApprovalIntent? Approval { get; init; } }
