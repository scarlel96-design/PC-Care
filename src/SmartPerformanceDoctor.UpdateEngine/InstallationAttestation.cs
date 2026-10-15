using System.IO.Compression;
using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;
public sealed record InstallationAttestationResult(bool Verified,bool RequiresReview,string Code);
public sealed record CommittedInstallation(string TransactionId,string InstallIdentity,SignedManifest Manifest,string Signature);
public sealed record InstallationPointer(string TransactionId,string TransactionIdentity,string InstallIdentity,string AttestationHash);

public sealed partial class UpdateTransaction
{
    private void RecordInstallation(string txDir,UpdateJournal journal,SignedManifest manifest,Stream package)
    {
        package.Position=0;using var zip=new ZipArchive(package,ZipArchiveMode.Read,true);
        using var signature=zip.GetEntry(SignedPackage.SignatureName)!.Open();
        var envelope=JsonSerializer.Deserialize<SignatureEnvelope>(signature)!;
        using(var record=WindowsFileBoundary.OpenWrite(txDir,"committed-attestation.json",true))
        {VerifyProtectedFile(record,txDir);record.Stream.Write(JsonSerializer.SerializeToUtf8Bytes(new CommittedInstallation(journal.TransactionId,journal.InstallIdentity,manifest,envelope.Signature)));record.Stream.Flush(true);}
        using var saved=WindowsFileBoundary.OpenRead(txDir,"committed-attestation.json");
        using var work=WindowsFileBoundary.PinDirectory(_workRoot);
        var name="installation-"+Guid.NewGuid().ToString("N")+".new";
        using(var pointer=WindowsFileBoundary.OpenWrite(_workRoot,name,true))
        {VerifyProtectedFile(pointer,_workRoot);pointer.Stream.Write(JsonSerializer.SerializeToUtf8Bytes(new InstallationPointer(journal.TransactionId,journal.TransactionIdentity,journal.InstallIdentity,saved.Hash())));pointer.Stream.Flush(true);}
        work.Validate();var target=Path.Combine(_workRoot,"installation-current.json");
        if(File.Exists(target))File.Replace(Path.Combine(_workRoot,name),target,Path.Combine(_workRoot,"installation-previous.json"));
        else File.Move(Path.Combine(_workRoot,name),target);
    }
    /// <summary>Inspect retained signed declarations. Do not repair or update any file.</summary>
    public InstallationAttestationResult InspectInstallation(string installRoot)
    {
        if(!File.Exists(Path.Combine(_workRoot,"installation-current.json")))return new(false,false,"INSTALLATION-NOT-ATTESTED");
        try
        {
            EnsureRoot(_workRoot);if(_strictAcl)VerifyProtectedAcl(_workRoot);
            using var install=WindowsFileBoundary.PinDirectory(installRoot);
            foreach(var name in new[]{"installation-current.json","installation-previous.json"})
            {
                if(!File.Exists(Path.Combine(_workRoot,name)))continue;
                using var pointerFile=WindowsFileBoundary.OpenRead(_workRoot,name);VerifyProtectedFile(pointerFile,_workRoot);
                if(pointerFile.Stream.Length>4096)throw new InvalidDataException("POINTER-LIMIT");
                using var document=JsonDocument.Parse(pointerFile.Stream);ValidateJsonProperties(document.RootElement);
                var pointer=document.RootElement.Deserialize<InstallationPointer>()!;
                if(pointer.InstallIdentity!=install.Identity||!Guid.TryParseExact(pointer.TransactionId,"N",out _))throw new InvalidDataException("POINTER-BINDING");
                var tx=Path.Combine(_workRoot,"tx-"+pointer.TransactionId);
                using var transaction=WindowsFileBoundary.PinDirectory(tx);
                if(transaction.Identity!=pointer.TransactionIdentity||!TryReadTrustedJournal(tx,installRoot,out var journal,out _)||journal is null)throw new InvalidDataException("POINTER-JOURNAL");
                if(journal.Stage==UpdateStage.Restored)continue;
                if(journal.Stage!=UpdateStage.Committed)throw new InvalidDataException("INSTALLATION-PENDING");
                using var record=WindowsFileBoundary.OpenRead(tx,"committed-attestation.json");VerifyProtectedFile(record,tx);
                if(record.Hash()!=pointer.AttestationHash||record.Stream.Length>2*1024*1024)throw new InvalidDataException("ATTESTATION-CHANGED");
                var binding=JsonSerializer.Deserialize<CommittedInstallation>(record.Stream)!;
                if(binding.TransactionId!=journal.TransactionId||binding.InstallIdentity!=install.Identity||binding.Manifest.PackageId!=journal.PackageId||
                    !SignedPackage.VerifyDeclaration(binding.Manifest,binding.Signature,_policy))throw new InvalidDataException("DECLARATION-UNTRUSTED");
                if(journal.Files.Count!=binding.Manifest.Files.Count)throw new InvalidDataException("DECLARATION-SCOPE");
                foreach(var expected in binding.Manifest.Files)
                {
                    var entry=journal.Files.Single(f=>f.Path==expected.Path);
                    if(entry.NewSha256!=expected.Sha256)throw new InvalidDataException("DECLARATION-HASH");
                    using var file=WindowsFileBoundary.OpenRead(installRoot,Relative(expected.Path));
                    try { file.Verify(expected.Length,expected.Sha256,entry.InstalledIdentity); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException)
                    { return new(false,true,"INSTALLATION-CONTENT-MISMATCH"); }
                }
                install.Validate();return new(true,false,"SIGNED-INSTALLATION-VERIFIED");
            }
            return new(false,false,"RESTORED-INSTALLATION-NOT-ATTESTED");
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or NullReferenceException)
        {return new(false,true,"INSTALLATION-REVIEW-REQUIRED");}
    }
}
