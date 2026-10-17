using System.Security.Cryptography;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;
namespace SmartPerformanceDoctor.Tests;
public sealed class WO24InstallationHistoryTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void ThreeSignedUpdatesVerifyEveryRetainedPath(bool samePath)
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var engine=f.Engine;
        for(var i=4;i<7;i++)Assert.Equal(UpdateStage.Committed,engine.Apply(f.Package("51.0."+i,"51.0."+(i+1),samePath?"same.bin":"file"+i+".bin"),f.Install,"51.0."+i).Stage);
        var result=engine.InspectInstallationHistory(f.Install);Assert.True(result.Verified);Assert.Equal(samePath?1:3,result.Files);Assert.Equal(3,result.Transactions);
    }
    [Fact]public void OlderDisjointPathTamperingRequiresReviewWithoutChangingBytes()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var e=f.Engine;
        Assert.Equal(UpdateStage.Committed,e.Apply(f.Package("51.0.4","51.0.5","older.bin"),f.Install,"51.0.4").Stage);
        Assert.Equal(UpdateStage.Committed,e.Apply(f.Package("51.0.5","51.0.6","newer.bin"),f.Install,"51.0.5").Stage);
        var file=Path.Combine(f.Install,"older.bin");File.WriteAllText(file,"changed");var before=File.ReadAllBytes(file);
        Assert.True(e.InspectInstallation(f.Install).Verified); // Existing API covers the current declaration only.
        Assert.False(e.InspectInstallationHistory(f.Install).Verified);Assert.Equal(before,File.ReadAllBytes(file));
    }
    [Fact]public void MissingHistoricalDeclarationIsNotInvented()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var e=f.Engine;
        e.Apply(f.Package("51.0.4","51.0.5","first.bin"),f.Install,"51.0.4");var first=Directory.GetFiles(f.Work,"committed-attestation.json",SearchOption.AllDirectories).Single();
        e.Apply(f.Package("51.0.5","51.0.6","second.bin"),f.Install,"51.0.5");File.Delete(first);
        Assert.False(e.InspectInstallationHistory(f.Install).Verified);
    }
    [Fact]public void EqualVersionPackageDoesNotCreateHistory()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var e=f.Engine;
        e.Apply(f.Package("51.0.4","51.0.5","first.bin"),f.Install,"51.0.4");
        Assert.NotEqual(UpdateStage.Committed,e.Apply(f.Package("51.0.5","51.0.5","duplicate.bin"),f.Install,"51.0.5").Stage);
        Assert.True(e.InspectInstallationHistory(f.Install).Verified);Assert.Single(Directory.GetDirectories(f.Work,"tx-*"));
    }
    [Fact]public void RestoredAttemptDoesNotOverrideReappliedSignedHistory()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var e=f.Engine;
        Assert.Equal(UpdateStage.Restored,e.Apply(f.Package("51.0.4","51.0.5","first.bin"),f.Install,"51.0.4",faultInjector:p=>{if(p==UpdateFaultPoint.BeforeCommittedJournal)throw new IOException("fixture");}).Stage);
        Assert.Equal(UpdateStage.Committed,e.Apply(f.Package("51.0.4","51.0.5","first.bin"),f.Install,"51.0.4").Stage);
        Assert.True(e.InspectInstallationHistory(f.Install).Verified);
    }
    [Fact]public void CrossVolumePublicationPreservesSourceWithoutCreatingDestination()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();
        var source=Path.Combine(f.Install,"volume.bin");var bytes=RandomNumberGenerator.GetBytes(4096);
        var other=Path.GetPathRoot(f.Install)!.Equals("C:\\",StringComparison.OrdinalIgnoreCase)?"D:\\":"C:\\";
        var destination=Path.Combine(other,"wo24-uncreated-"+Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(destination));
        using(var file=WindowsFileBoundary.OpenWrite(f.Install,"volume.bin",create:true,deletable:true))
        {
            file.Stream.Write(bytes);file.Stream.Flush(true);
            var error=Assert.Throws<IOException>(()=>file.PublishOpenedFile(destination,"same.bin"));
            Assert.Contains("PUBLISH-SAME-VOLUME-REQUIRED",error.Message);file.Validate();
        }
        Assert.False(Directory.Exists(destination));Assert.Equal(bytes,File.ReadAllBytes(source));
    }
    [Fact]public void SignedButDiscontinuousHistoryRequiresReview()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var e=f.Engine;
        e.Apply(f.Package("51.0.4","51.0.5","first.bin"),f.Install,"51.0.4");
        e.Apply(f.Package("51.0.6","51.0.7","second.bin"),f.Install,"51.0.6");
        Assert.False(e.InspectInstallationHistory(f.Install).Verified);
    }

}
public sealed class WO24RebootEvidenceTests
{
    private static QualificationObservation Before=>new("operation","machine","boot1","source","root","hash",true,"NoPendingTransaction");
    [Fact]public void SameBootCannotClaimReboot(){var r=InstallationQualificationComparison.Compare(Before,Before,true);Assert.False(r.Verified);Assert.Equal("RebootNotObserved",r.State);}
    [Fact]public void ChangedBootAndVerifiedBytesAreExplicitObservations(){var r=InstallationQualificationComparison.Compare(Before,Before with{BootBinding="boot2"},true);Assert.True(r.Verified);Assert.True(r.RebootObserved);}
    [Theory][InlineData("run")][InlineData("machine")][InlineData("source")][InlineData("root")]
    public void UnrelatedEvidenceCannotMatch(string field)
    {var a=field switch{"run"=>Before with{RunId="other"},"machine"=>Before with{MachineBinding="other"},"source"=>Before with{Source="other"},_=>Before with{RootIdentity="other"}};Assert.False(InstallationQualificationComparison.Compare(Before,a,false).Verified);}
    [Theory][InlineData("RecoveryAvailable")][InlineData("RecoveryInProgress")][InlineData("RecoveryRequired")][InlineData("RecoveryFailed")]
    public void PendingOrFailedRecoveryIsNotComplete(string state){Assert.False(InstallationQualificationComparison.Compare(Before,Before with{RecoveryState=state},false).Verified);}
    [Fact]public void ChangedContentIsNotRestoration(){Assert.False(InstallationQualificationComparison.Compare(Before,Before with{ManifestDigest="other"},false).Verified);}
    [Fact]public void MissingAuthenticationCannotPass(){Assert.False(InstallationQualificationComparison.Compare(Before,Before with{BytesVerified=false},false).Verified);}
}
