using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using SmartPerformanceDoctor.App.Services.Update;
using Xunit;
namespace SmartPerformanceDoctor.Tests;

public sealed class WO23CommitAttestationCrashTests
{
    [Theory]
    [InlineData(UpdateFaultPoint.AfterFinalHashVerification)]
    [InlineData(UpdateFaultPoint.AttestationCreated)]
    [InlineData(UpdateFaultPoint.AttestationPartialWrite)]
    [InlineData(UpdateFaultPoint.AttestationFlushed)]
    [InlineData(UpdateFaultPoint.InstallationPointerCreated)]
    [InlineData(UpdateFaultPoint.InstallationPointerFlushed)]
    [InlineData(UpdateFaultPoint.BeforeInstallationPointerReplace)]
    [InlineData(UpdateFaultPoint.AfterInstallationPointerReplace)]
    [InlineData(UpdateFaultPoint.BeforeCommittedJournal)]
    public void PublicationNeverPrecedesCommittedJournalTrust(UpdateFaultPoint point)
    {
        using var f=new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");
        File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        bool observed=false;
        var result=f.Transaction.Apply(f.Package(),f.Install,"51.0.4",faultInjector:p=>
        {
            if(p!=point)return; observed=true;
            var journal=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(Directory.GetFiles(f.Work,"journal.json",SearchOption.AllDirectories).Single()))!;
            Assert.NotEqual(UpdateStage.Committed,journal.Stage);
            Assert.False(f.Transaction.InspectInstallation(f.Install).Verified);
            throw new IOException("WO23 injected publication failure");
        });
        Assert.True(observed);Assert.Equal(UpdateStage.Restored,result.Stage);
        Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        Assert.Equal("old-second",File.ReadAllText(Path.Combine(f.Install,"second.txt")));
    }
    [Theory][InlineData(UpdateFaultPoint.AfterCommittedJournal)][InlineData(UpdateFaultPoint.BeforeSuccessReturn)]
    public void CommittedCheckpointHasAuthenticatedDeclarationAndActualBytes(UpdateFaultPoint point)
    {
        using var f=new SignedUpdateTests.Fixture();bool seen=false;
        var result=f.Transaction.Apply(f.Package(),f.Install,"51.0.4",faultInjector:p=>
        {if(p==point){seen=true;Assert.True(File.Exists(Path.Combine(f.Work,"installation-current.json")));Assert.Single(Directory.GetFiles(f.Work,"committed-attestation.json",SearchOption.AllDirectories));}});
        Assert.True(seen);Assert.Equal(UpdateStage.Committed,result.Stage);Assert.True(f.Transaction.InspectInstallation(f.Install).Verified);
    }
    [Fact] public void CurrentPointerCorruptionDoesNotSelectPreviousAsAuthority()
    {
        using var f=new SignedUpdateTests.Fixture();Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);
        File.Copy(Path.Combine(f.Work,"installation-current.json"),Path.Combine(f.Work,"installation-previous.json"));
        File.WriteAllText(Path.Combine(f.Work,"installation-current.json"),"{");
        Assert.False(f.Transaction.InspectInstallation(f.Install).Verified);
        Assert.True(f.Transaction.InspectInstallationTrust(f.Install).RequiresReview);
    }
}
public sealed class WO23MemoryAllocationTests
{
    [Fact] public void RepeatedIdentityValidationHasBoundedAllocationAndRetainsReplacementDefense()
    {
        var root=Path.Combine(Path.GetTempPath(),"wo23-id-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"sample.bin"),"synthetic");
            using var lease=WindowsFileBoundary.OpenRead(root,"sample.bin");lease.Validate();
            var start=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<100;i++)lease.Validate(lease.Identity);
            var bytes=GC.GetAllocatedBytesForCurrentThread()-start;
            Assert.True(bytes<8*1024*1024,$"Identity validation allocated {bytes} bytes");
            Assert.Throws<IOException>(()=>File.Delete(Path.Combine(root,"sample.bin")));
            Assert.Equal(Convert.ToHexString(SHA256.HashData("synthetic"u8)),lease.Hash());
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact] public void LongFinalPathRetriesWithoutTruncation()
    {
        var root=Path.Combine(Path.GetTempPath(),"wo23-long-"+Guid.NewGuid().ToString("N"));
        var deep=Path.Combine(root,string.Join(Path.DirectorySeparatorChar,Enumerable.Repeat(new string('x',90),6)));
        Directory.CreateDirectory(deep);
        try{File.WriteAllText(Path.Combine(deep,"sample.bin"),"synthetic");using var lease=WindowsFileBoundary.OpenRead(root,Path.GetRelativePath(root,Path.Combine(deep,"sample.bin")));lease.Validate();Assert.Equal(64,lease.Hash().Length);}
        finally{Directory.Delete(root,true);}
    }
}
public sealed class WO23UiStateRegressionTests
{
    [Fact] public void CompletionRejectsPreviouslyQueuedReports()
    {var gate=new UpdateProgressGate();var report=gate.Capture();gate.Invalidate();Assert.False(gate.IsCurrent(report));}
    [Fact] public void RestartRejectsOldReportAndAcceptsOnlyNewEpoch()
    {var gate=new UpdateProgressGate();var old=gate.Capture();gate.Invalidate();var next=gate.Capture();Assert.False(gate.IsCurrent(old));Assert.True(gate.IsCurrent(next));}
}
public sealed class WO23CancellationCleanupTests
{
    [Theory][InlineData(UpdateFaultPoint.DuringStageExtraction)][InlineData(UpdateFaultPoint.DuringBackupCreation)]
    public void CancelledPreparationAllowsASeparateNormalUpdate(UpdateFaultPoint point)
    {
        using var f=new SignedUpdateTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");using var c=new CancellationTokenSource();
        var result=f.Transaction.Apply(f.Package(),f.Install,"51.0.4",faultInjector:p=>{if(p==point)c.Cancel();},cancellationToken:c.Token);
        Assert.Equal("CANCELLED-PREPARATION",result.Code);Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);Assert.True(f.Transaction.InspectInstallationTrust(f.Install).Verified);
    }
}
public sealed class WO23ApprovalReconciliationTests
{
    [Fact] public void PartialReservationIsPreservedAndAcknowledgementRemainsUnknown()
    {
        var(f,_,r,h)=WO22ApprovalLifecycleTests.Create();using(f)
        {
            var request=JsonSerializer.Deserialize<LegacyRestoreRequest>(File.ReadAllText(r))!;
            var receipt=Path.Combine(f.Work,"legacy-approval-"+request.Approval!.RequestId+".json");File.WriteAllText(receipt,"{\"RequestId\":");
            var before=File.ReadAllBytes(receipt);var result=f.Transaction.InspectLegacyApproval(f.Install,r,h);
            Assert.Equal(LegacyApprovalState.RecoveryReviewRequired,result.State);Assert.Null(result.CallerAcknowledged);Assert.Equal(before,File.ReadAllBytes(receipt));
            Assert.Throws<IOException>(()=>f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true));
        }
    }
}
