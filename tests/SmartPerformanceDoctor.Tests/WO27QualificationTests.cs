using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;
namespace SmartPerformanceDoctor.Tests;

public sealed class WO27RecoveryDispositionTests
{
    [Fact] public void RecoveryMutationSurvivesApplyErrorResultComposition()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();
        var path=Path.Combine(f.Install,"target.bin");File.WriteAllText(path,"original");
        var result=f.Engine.Apply(f.Package("51.0.4","51.0.5","target.bin"),f.Install,"51.0.4",faultInjector:p=>
        {
            if(p==UpdateFaultPoint.AfterApplyingJournal){File.WriteAllText(path,"target.bin");throw new IOException("SYNTHETIC-EXTERNAL-UPDATED-CONTENT");}
        });
        Assert.Equal(UpdateStage.Restored,result.Stage);Assert.False(result.ApplyMutationStarted);
        Assert.True(result.RecoveryMutationStarted);Assert.True(result.AnyMutationStarted);
        Assert.Equal("RecoveryCompleted",result.RecoveryDisposition);Assert.Equal("original",File.ReadAllText(path));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void UnknownContentRemainsPreservedRegardlessOfAppliedFlag(bool applied)
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var path=Path.Combine(f.Install,"target.bin");File.WriteAllText(path,"original");
        var package=f.Package("51.0.4","51.0.5","target.bin");
        f.Engine.Apply(package,f.Install,"51.0.4",faultInjector:p=>{if(p is UpdateFaultPoint.AfterApplyingJournal or UpdateFaultPoint.AfterRollbackStarted)throw new IOException("PAUSE");});
        var tx=Directory.GetDirectories(f.Work,"tx-*").Single();var journalPath=Path.Combine(tx,"journal.json");
        var journal=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(journalPath))!;journal.Files[0].Applied=applied;File.WriteAllText(journalPath,JsonSerializer.Serialize(journal));
        File.WriteAllText(path,"unknown bytes");var backup=SHA256.HashData(File.ReadAllBytes(Path.Combine(tx,"backup","target.bin")));
        var observed=f.Engine.ReconcileRecovery(f.Install);Assert.False(observed.CanRequestRecovery);Assert.Contains(observed.Files,x=>x.State=="UnknownContent");
        Assert.Equal(UpdateStage.RecoveryRequired,f.Engine.ResumeVerifiedRecovery(f.Install).Stage);
        Assert.Equal(UpdateStage.RecoveryRequired,f.Engine.Recover(f.Install).Stage);
        Assert.Equal("unknown bytes",File.ReadAllText(path));Assert.Equal(backup,SHA256.HashData(File.ReadAllBytes(Path.Combine(tx,"backup","target.bin"))));
    }
    [Fact] public void SyntheticCopyObserverRejectsProductionAndUnmarkedRoots()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();
        Assert.Throws<InvalidOperationException>(()=>f.Engine.ObserveSyntheticRecoveryCopy(_=>{}));
        var production=new UpdateTransaction(new PublisherTrustPolicy([]),f.Work,true);
        Assert.Throws<InvalidOperationException>(()=>production.ObserveSyntheticRecoveryCopy(_=>{}));
    }
}

public sealed class WO27HistoryHandleBudgetTests
{
    [Theory][InlineData(1)][InlineData(10)]
    public void SharingRetainsIdentityAndReleasesHandlesAfterInspection(int count)
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var e=f.Engine;
        for(var i=0;i<count;i++)Assert.Equal(UpdateStage.Committed,e.Apply(f.Package("51.0."+(i+4),"51.0."+(i+5),"file.bin"),f.Install,"51.0."+(i+4)).Stage);
        var result=e.InspectInstallationHistory(f.Install);
        Assert.True(result.Verified);Assert.Equal(count,result.Transactions);
        Assert.InRange(result.RetainedDirectoryHandles,1,count+20);Assert.True(result.ReusedDirectoryHandles>count);
        Assert.InRange(result.RetainedFileHandles,2*count,3*count+4);
        var declaration=Directory.GetFiles(f.Work,"committed-attestation.json",SearchOption.AllDirectories).First();
        using var exclusive=new FileStream(declaration,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
    }
    [Fact] public void SharedAncestorsKeepTheOriginalNoDeleteBoundary()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var parent=Path.Combine(f.Root,"parent");Directory.CreateDirectory(parent);
        File.WriteAllText(Path.Combine(parent,"a"),"a");File.WriteAllText(Path.Combine(parent,"b"),"b");
        using(var scope=WindowsFileBoundary.RetainSharedDirectoryHandles())
        {
            using var a=WindowsFileBoundary.OpenRead(parent,"a");using var b=WindowsFileBoundary.OpenRead(parent,"b");
            Assert.Throws<IOException>(()=>Directory.Move(parent,parent+"-replaced"));
            a.Validate();b.Validate();scope.Validate();Assert.True(scope.ReusedHandles>0);
        }
        Directory.Move(parent,parent+"-released");
    }
    [Theory][InlineData("declaration")][InlineData("journal")][InlineData("content")][InlineData("pointer")][InlineData("identity")]
    public void ReducedHandleCountNeverPromotesTamperedHistory(string change)
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var e=f.Engine;
        Assert.Equal(UpdateStage.Committed,e.Apply(f.Package("51.0.4","51.0.5","file.bin"),f.Install,"51.0.4").Stage);
        var tx=Directory.GetDirectories(f.Work,"tx-*").Single();var target=Path.Combine(f.Install,"file.bin");
        if(change=="declaration")File.Delete(Path.Combine(tx,"committed-attestation.json"));
        if(change=="journal")File.WriteAllText(Path.Combine(tx,"journal.json"),"{");
        if(change=="content")File.WriteAllText(target,"changed");
        if(change=="pointer")File.WriteAllText(Path.Combine(f.Work,"installation-current.json"),"{}");
        if(change=="identity"){File.Move(target,target+".saved");File.WriteAllText(target,"file.bin");}
        Assert.False(e.InspectInstallationHistory(f.Install).Verified);
    }
}

public sealed class WO27DiagnosticPrivacyTests
{
    [Fact] public void MutationFieldsAreNumericOrBooleanAndPrivateIdentifiersStayExcluded()
    {
        var raw=UpdateDiagnosticExport.Serialize(new TransactionResult(UpdateStage.RecoveryRequired,"private-path","private-id",0){ApplyMutationStarted=false,RecoveryMutationStarted=true,AnyMutationStarted=true,Failure="private-secret"});
        using var json=JsonDocument.Parse(raw);Assert.True(json.RootElement.GetProperty("RecoveryMutationStarted").GetBoolean());
        Assert.False(json.RootElement.GetProperty("ApplyMutationStarted").GetBoolean());Assert.DoesNotContain("private",raw);
    }
    [Fact] public void UiRequiresReadOnlyRecheckBeforeRecoveryRequest()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!Directory.Exists(Path.Combine(root.FullName,"src")))root=root.Parent;
        var ui=File.ReadAllText(Path.Combine(root!.FullName,"src/SmartPerformanceDoctor.App/UpdateRecoveryWindow.cs"));
        Assert.Contains("_recoverRequest.IsEnabled = observation.CanRequestRecovery",ui);
        Assert.Contains("if (!observation.CanRequestRecovery)",ui);Assert.Contains("관리자 복구 요청\", IsEnabled = false",ui);
    }
}

