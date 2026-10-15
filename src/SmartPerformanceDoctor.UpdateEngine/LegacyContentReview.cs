using System.Security.AccessControl;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record LegacyContentReviewItem(string Path, string Result, string CurrentIdentity, long CurrentLength,
    string CurrentHash, string CurrentSecurity, string BackupIdentity, string BackupHash, bool BackupAuthenticated);
public sealed record LegacyContentReview(string TransactionId, string Result, string JournalHash,
    string SignedPackageIdentity, string SignedPackageHash, IReadOnlyList<LegacyContentReviewItem> Targets)
{
    public bool CanExecute => false;
}

public sealed partial class UpdateTransaction
{
    /// <summary>Preserve uncertain content. Return observations without granting restoration authority.</summary>
    public LegacyContentReview ReviewLegacyContent(string installRoot,string transactionId,string packagePath,
        string previousVersion,LegacyRecoveryPlan? prior = null)
    {
        var items = new List<LegacyContentReviewItem>(); var journalHash=""; var packageIdentity=""; var packageHash="";
        LegacyContentReview Result(string code)=>new(transactionId,code,journalHash,packageIdentity,packageHash,items);
        try
        {
            var diagnostics=DiagnoseLegacy(installRoot);
            if(diagnostics.Count!=1)return Result(diagnostics.Count>1?"MultiplePendingTransactions":"ManualReviewRequired");
            var diagnosis=diagnostics.Single();journalHash=diagnosis.JournalHash;
            if(diagnosis.TransactionId!=transactionId||!diagnosis.Code.StartsWith("READONLY-MANUAL-REVIEW",StringComparison.Ordinal))return Result("ManualReviewRequired");
            using var package=WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(packagePath))!,Path.GetFileName(packagePath));
            packageIdentity=package.Identity;packageHash=package.Hash();
            var verified=SignedPackage.Verify(package.Stream,previousVersion,_policy);
            if(!verified.CanApply||verified.Manifest is null)return Result("SignatureEvidenceMissing");
            var tx=Path.Combine(_workRoot,"tx-"+transactionId);
            using var journalFile=WindowsFileBoundary.OpenRead(tx,"journal.json");VerifyProtectedFile(journalFile,tx);
            if(journalFile.Hash()!=journalHash)return Result("IdentityChanged");
            var journal=ReadJournalFile(tx,"journal.json")!;
            if(verified.Manifest.ToVersion!=journal.FromVersion)return Result("SignatureEvidenceMissing");
            foreach(var observed in diagnosis.Files)
            {
                var expected=verified.Manifest.Files.SingleOrDefault(f=>f.Path==observed.Path);
                var backupOk=false; var backupId=observed.BackupIdentity; var backupHash=observed.BackupHash;
                try
                {
                    using var backup=WindowsFileBoundary.OpenRead(Path.Combine(tx,"backup"),Relative(observed.Path));
                    VerifyProtectedFile(backup,Path.Combine(tx,"backup"));backupId=backup.Identity;backupHash=backup.Hash();
                    backupOk=expected is not null && backup.Stream.Length==expected.Length && backupHash==expected.Sha256;
                }
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
                using var current=WindowsFileBoundary.OpenRead(installRoot,Relative(observed.Path));
                var hash=current.Hash();var security=current.ReadSecurity().GetSecurityDescriptorSddlForm(AccessControlSections.All);
                var old=prior?.Targets.SingleOrDefault(t=>t.Path==observed.Path);
                var classification=ClassifyLegacyContent(tx,journal,observed with {InstalledHash=hash},current.Stream.Length,expected);
                var state=!backupOk?"BackupUnavailable":classification=="UnknownContent"?"BackupVerifiedButCurrentUnknown":"CurrentContentVerified";
                if(old is not null && old.CurrentIdentity!=current.Identity)state="IdentityChanged";
                else if(old is not null && old.CurrentSecurity!=security)state="SecurityChanged";
                items.Add(new(observed.Path,state,current.Identity,current.Stream.Length,hash,security,backupId,backupHash,backupOk));
            }
            journalFile.Validate();package.Validate();
            return Result(items.All(i=>i.Result=="CurrentContentVerified")?"CurrentContentVerified":"ManualReviewRequired");
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {return Result("ManualReviewRequired");}
    }
}
