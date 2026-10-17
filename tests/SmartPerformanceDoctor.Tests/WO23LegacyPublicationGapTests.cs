using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;
namespace SmartPerformanceDoctor.Tests;
public sealed class WO23LegacyPublicationGapTests
{
    internal sealed class Fixture:IDisposable
    {
        private readonly ECDsa _key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public string Root{get;}=Path.Combine(Path.GetTempPath(),"wo23-gap-"+Guid.NewGuid().ToString("N"));
        public string Install=>Path.Combine(Root,"install");public string Work=>Path.Combine(Root,"work");
        public UpdateTransaction Engine=>new(new PublisherTrustPolicy([new TrustedPublisherKey("WO23-Fixture","ephemeral","stable",_key.ExportSubjectPublicKeyInfo())]),Work,false);
        public Fixture(){Directory.CreateDirectory(Install);Directory.CreateDirectory(Work);}
        public string Package(string from,string to,string name)
        {
            var data=System.Text.Encoding.UTF8.GetBytes(name);var m=new SignedManifest{Publisher="WO23-Fixture",KeyId="ephemeral",PackageId=Guid.NewGuid().ToString("N"),FromVersion=from,ToVersion=to,MinimumSupportedVersion="51.0.0",CreatedAt=DateTimeOffset.UtcNow.AddMinutes(-1),ExpiresAt=DateTimeOffset.UtcNow.AddHours(1),Files=[new SignedFile{Path="payload/"+name,Length=data.Length,Sha256=Convert.ToHexString(SHA256.HashData(data))}]};
            var path=Path.Combine(Root,Guid.NewGuid().ToString("N")+".spdup");using var z=new ZipArchive(File.Create(path),ZipArchiveMode.Create);
            void Add(string p,byte[] b){using var s=z.CreateEntry(p).Open();s.Write(b);}
            Add(SignedPackage.ManifestName,JsonSerializer.SerializeToUtf8Bytes(m));Add(SignedPackage.SignatureName,JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope{Signature=Convert.ToBase64String(_key.SignData(SignedPackage.CanonicalBytes(m),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))}));Add("payload/"+name,data);return path;
        }
        public void Dispose(){_key.Dispose();Directory.Delete(Root,true);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void OldPointerCannotVerifyDisjointCommittedSuccessor(bool removeSuccessorDeclaration)
    {
        using var f=new Fixture();var engine=f.Engine;
        Assert.Equal(UpdateStage.Committed,engine.Apply(f.Package("51.0.4","51.0.5","first.bin"),f.Install,"51.0.4").Stage);
        var oldPointer=File.ReadAllBytes(Path.Combine(f.Work,"installation-current.json"));
        Assert.Equal(UpdateStage.Committed,engine.Apply(f.Package("51.0.5","51.0.6","second.bin"),f.Install,removeSuccessorDeclaration?"51.0.05":"51.0.5").Stage);
        var current=JsonSerializer.Deserialize<InstallationPointer>(File.ReadAllText(Path.Combine(f.Work,"installation-current.json")))!;
        File.WriteAllBytes(Path.Combine(f.Work,"installation-current.json"),oldPointer);
        if(removeSuccessorDeclaration)File.Delete(Path.Combine(f.Work,"tx-"+current.TransactionId,"committed-attestation.json"));
        var next=f.Package("51.0.6","51.0.7","third.bin");
        var before=Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>SHA256.HashData(File.ReadAllBytes(x)));
        Assert.False(engine.InspectInstallation(f.Install).Verified);Assert.Equal(InstallationTrustState.RecoveryRequired,engine.InspectInstallationTrust(f.Install).State);
        Assert.Equal(UpdateRecoveryState.RecoveryRequired,engine.InspectRecovery(f.Install).State);
        Assert.Equal("INSTALLATION-RECOVERY-REQUIRED",engine.Apply(next,f.Install,"51.0.6").Code);
        Assert.Equal(before.Count,Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).Length);
        Assert.All(before,x=>Assert.Equal(x.Value,SHA256.HashData(File.ReadAllBytes(x.Key))));
    }
    [Fact] public void LatestPointerStillVerifiesNormalDisjointUpdates()
    {
        using var f=new Fixture();var e=f.Engine;
        for(var i=4;i<7;i++)Assert.Equal(UpdateStage.Committed,e.Apply(f.Package("51.0."+i,"51.0."+(i+1),"file"+i+".bin"),f.Install,"51.0."+i).Stage);
        Assert.True(e.InspectInstallationTrust(f.Install).Verified);
    }
}
