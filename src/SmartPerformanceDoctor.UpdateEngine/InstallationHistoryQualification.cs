using System.Text.Json;
namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record InstallationHistoryResult(bool Verified, string State, string Code, int Transactions, int Files);
public sealed partial class UpdateTransaction
{
    /// <summary>Verify retained signed history. This read-only result grants no execution authority.</summary>
    public InstallationHistoryResult InspectInstallationHistory(string installRoot)
    {
        var current=InspectInstallation(installRoot);
        if(!current.Verified)return new(false,"RecoveryRequired",current.Code,0,0);
        try
        {
            using var install=WindowsFileBoundary.PinDirectory(installRoot);
            using var work=WindowsFileBoundary.PinDirectory(_workRoot);
            var records=new List<(Version From,Version To,UpdateJournal Journal,CommittedInstallation Declaration)>();
            foreach(var directory in Directory.EnumerateDirectories(_workRoot,"tx-*"))
            {
                if(records.Count>=4096)throw new InvalidDataException("HISTORY-LIMIT");
                if(!TryReadTrustedJournal(directory,installRoot,out var journal,out _)||journal is null)
                    throw new InvalidDataException("HISTORY-JOURNAL");
                if(journal.Stage==UpdateStage.Restored)continue;
                if(journal.Stage!=UpdateStage.Committed)throw new InvalidDataException("HISTORY-INCOMPLETE");
                using var record=WindowsFileBoundary.OpenRead(directory,"committed-attestation.json");
                VerifyProtectedFile(record,directory);
                if(record.Stream.Length>2*1024*1024)throw new InvalidDataException("HISTORY-LIMIT");
                var declaration=JsonSerializer.Deserialize<CommittedInstallation>(record.Stream)!;
                if(declaration.TransactionId!=journal.TransactionId||declaration.InstallIdentity!=install.Identity||
                   declaration.Manifest.PackageId!=journal.PackageId||!SignedPackage.VerifyDeclaration(declaration.Manifest,declaration.Signature,_policy))
                    throw new InvalidDataException("HISTORY-SIGNATURE");
                if(!Version.TryParse(declaration.Manifest.FromVersion,out var from)||!Version.TryParse(declaration.Manifest.ToVersion,out var to)||from>=to)
                    throw new InvalidDataException("HISTORY-VERSION");
                if(declaration.Manifest.Files.Count!=journal.Files.Count)throw new InvalidDataException("HISTORY-SCOPE");
                foreach(var file in declaration.Manifest.Files)
                {
                    var entry=journal.Files.Single(x=>x.Path==file.Path);
                    if(entry.NewSha256!=file.Sha256||entry.NewLength!=file.Length)throw new InvalidDataException("HISTORY-SCOPE");
                }
                records.Add((from,to,journal,declaration));
            }
            var ordered=records.OrderBy(x=>x.To).ToArray();
            for(var i=1;i<ordered.Length;i++)
                if(ordered[i-1].To!=ordered[i].From)throw new InvalidDataException("HISTORY-CHAIN-REVIEW");
            var expected=new Dictionary<string,(SignedFile File,string Identity)>(StringComparer.OrdinalIgnoreCase);
            foreach(var record in ordered)foreach(var file in record.Declaration.Manifest.Files)
                expected[file.Path]=(file,record.Journal.Files.Single(x=>x.Path==file.Path).InstalledIdentity);
            foreach(var entry in expected)
            {
                using var file=WindowsFileBoundary.OpenRead(installRoot,Relative(entry.Key));
                VerifyInstallationFile(file,entry.Value.File.Length,entry.Value.File.Sha256,entry.Value.Identity);
            }
            install.Validate();work.Validate();
            return new(true,"InstallationVerified","RETAINED-SIGNED-HISTORY-VERIFIED",records.Count,expected.Count);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException or NullReferenceException)
        {return new(false,"ManualReviewRequired","HISTORY-REVIEW:"+ex.GetType().Name,0,0);}
    }
}

public sealed record QualificationObservation(string RunId,string MachineBinding,string BootBinding,string Source,
    string RootIdentity,string ManifestDigest,bool BytesVerified,string RecoveryState);
public sealed record QualificationComparison(bool Verified,string State,bool RebootObserved);
public static class InstallationQualificationComparison
{
    public static QualificationComparison Compare(QualificationObservation before,QualificationObservation after,bool requireReboot)
    {
        if(string.IsNullOrWhiteSpace(before.RunId)||before.RunId!=after.RunId||
            string.IsNullOrWhiteSpace(before.MachineBinding)||before.MachineBinding!=after.MachineBinding||
            string.IsNullOrWhiteSpace(before.Source)||before.Source!=after.Source||
            string.IsNullOrWhiteSpace(before.RootIdentity)||before.RootIdentity!=after.RootIdentity)
            return new(false,"EvidenceBindingMismatch",false);
        var reboot=!string.IsNullOrWhiteSpace(before.BootBinding)&&!string.IsNullOrWhiteSpace(after.BootBinding)&&before.BootBinding!=after.BootBinding;
        if(requireReboot&&!reboot)return new(false,"RebootNotObserved",false);
        if(!before.BytesVerified||!after.BytesVerified||string.IsNullOrWhiteSpace(before.ManifestDigest)||before.ManifestDigest!=after.ManifestDigest)
            return new(false,"ContentReviewRequired",reboot);
        if(after.RecoveryState is not ("NoPendingTransaction" or "RecoveryCompleted"))return new(false,"RecoveryReviewRequired",reboot);
        return new(true,"ObservedBytesMatch",reboot);
    }
}
