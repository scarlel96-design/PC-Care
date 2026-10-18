using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;
namespace SmartPerformanceDoctor.Tests;

public sealed class WO26RecoveryStateClassificationTests
{
    [Theory][InlineData("read")][InlineData("write")][InlineData("exclusive")]
    public void SharingConflictPreservesOriginalsAndSeparatesMutation(string mode)
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        var path = Path.Combine(f.Install,"target.bin"); File.WriteAllText(path,"original");
        using (var holder = new FileStream(path,FileMode.Open,mode=="write"?FileAccess.Write:FileAccess.Read,mode=="exclusive"?FileShare.None:FileShare.Read))
        {
            var result=f.Engine.Apply(f.Package("51.0.4","51.0.5","target.bin"),f.Install,"51.0.4");
            Assert.Equal(32,result.Diagnostic!.NativeErrorCode); Assert.False(result.AnyMutationStarted);
            Assert.Equal("PreMutationRejected",result.RecoveryDisposition); Assert.Equal(UpdateStage.Restored,result.Stage);
        }
        Assert.Equal("original",File.ReadAllText(path));
    }
    [Fact] public void ReadOnlyOriginalDoesNotRequireRollbackWrite()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var path=Path.Combine(f.Install,"target.bin");File.WriteAllText(path,"original");
        try
        {
            File.SetAttributes(path,FileAttributes.ReadOnly);
            var result=f.Engine.Apply(f.Package("51.0.4","51.0.5","target.bin"),f.Install,"51.0.4");
            Assert.Equal(5,result.Diagnostic!.NativeErrorCode);Assert.Equal("PreMutationRejected",result.RecoveryDisposition);
            Assert.True(result.OriginalBytesVerified); Assert.False(result.AnyMutationStarted);
            Assert.Equal("original",File.ReadAllText(path));
        }
        finally {File.SetAttributes(path,FileAttributes.Normal);}
    }
    [Theory][InlineData("none")][InlineData("identity")][InlineData("backup")][InlineData("package")][InlineData("content")]
    public void ReconciliationRechecksEvidenceBeforeExplicitResume(string change)
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var path=Path.Combine(f.Install,"target.bin");File.WriteAllText(path,"original");
        var package=f.Package("51.0.4","51.0.5","target.bin");
        var result=f.Engine.Apply(package,f.Install,"51.0.4",faultInjector:p=>
        {
            if(p==UpdateFaultPoint.AfterApplyingJournal)throw new IOException("INJECTED");
            if(p==UpdateFaultPoint.AfterRollbackStarted)throw new IOException("PAUSE-RECOVERY");
        });
        Assert.Equal(UpdateStage.RecoveryRequired,result.Stage);
        var tx=Directory.GetDirectories(f.Work,"tx-*").Single();
        if(change=="identity"){File.Move(path,path+".old");File.WriteAllText(path,"original");}
        if(change=="backup")File.WriteAllText(Path.Combine(tx,"backup","target.bin"),"modified");
        if(change=="package")File.WriteAllText(Path.Combine(tx,"package.spdup"),"invalid");
        if(change=="content")File.WriteAllText(path,"external content");
        var before=File.ReadAllBytes(path);var journal=File.ReadAllBytes(Path.Combine(tx,"journal.json"));
        var observed=f.Engine.ReconcileRecovery(f.Install);
        Assert.Equal(before,File.ReadAllBytes(path));Assert.Equal(journal,File.ReadAllBytes(Path.Combine(tx,"journal.json")));
        Assert.Equal(change=="none",observed.CanRequestRecovery);
        var resumed=f.Engine.ResumeVerifiedRecovery(f.Install);
        if(change=="none")
        {
            Assert.Equal(UpdateStage.Restored,resumed.Stage);Assert.False(resumed.AnyMutationStarted);
            Assert.Equal("OriginalStateVerified",resumed.RecoveryDisposition);Assert.Equal("original",File.ReadAllText(path));
            Assert.Equal(UpdateRecoveryState.RecoveryCompleted,f.Engine.InspectRecovery(f.Install).State);
        }
        else {Assert.Equal(UpdateStage.RecoveryRequired,resumed.Stage);Assert.Equal(before,File.ReadAllBytes(path));}
    }
    [Fact] public void CompletedJournalWithChangedOriginalStillRequiresReview()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();var path=Path.Combine(f.Install,"target.bin");File.WriteAllText(path,"original");
        f.Engine.Apply(f.Package("51.0.4","51.0.5","target.bin"),f.Install,"51.0.4",faultInjector:p=>{if(p==UpdateFaultPoint.BeforeReplacement)throw new IOException("INJECTED");});
        File.WriteAllText(path,"external change");
        Assert.Equal("ManualReviewRequired",f.Engine.ReconcileRecovery(f.Install).State);
        Assert.Equal(UpdateStage.RecoveryRequired,f.Engine.ResumeVerifiedRecovery(f.Install).Stage);
        Assert.Equal("external change",File.ReadAllText(path));
    }
    [Fact] public void ActualRestorationReportsMutationSeparately()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"target.bin"),"original");
        var result=f.Engine.Apply(f.Package("51.0.4","51.0.5","target.bin"),f.Install,"51.0.4",faultInjector:p=>{if(p==UpdateFaultPoint.AfterFirstReplacement)throw new IOException("INJECTED");});
        Assert.True(result.AnyMutationStarted);Assert.True(result.OriginalBytesVerified);Assert.Equal("RecoveryCompleted",result.RecoveryDisposition);
        Assert.Equal("original",File.ReadAllText(Path.Combine(f.Install,"target.bin")));
    }
}

public sealed class WO26DiagnosticPrivacyTests
{
    [Fact] public void ExportExcludesAllFreeFormAndMachineBoundFields()
    {
        var secret="C:/Users/private-person/vault/recovery-code";
        var diagnostic=UpdateFailureDiagnostic.Capture(new IOException(secret),secret,UpdateStage.Applying,secret,secret,secret,true,true);
        var raw=UpdateDiagnosticExport.Serialize(new TransactionResult(UpdateStage.RecoveryRequired,secret,secret,1){Failure=secret,Diagnostic=diagnostic,RecoveryDiagnostic=diagnostic,RecoveryDisposition=secret});
        Assert.DoesNotContain("private-person",raw);Assert.DoesNotContain("recovery-code",raw);Assert.DoesNotContain(secret,raw);
        using var parsed=JsonDocument.Parse(raw);Assert.True(parsed.RootElement.TryGetProperty("Primary",out _));
        Assert.Contains("NOT OBSERVED",raw);
    }
    [Theory][InlineData("RecoveryDeferred")][InlineData("RecoveryRequired")][InlineData("ManualReviewRequired")]
    public void RecoveryTextDoesNotClaimCompletion(string state)
    {
        var text=UpdateStateText.Recovery(new(state,"SYNTHETIC","",true,false,[]));
        Assert.DoesNotContain("복구가 완료",text);Assert.True(text.Length>20);
    }
}

public sealed class WO26DeploymentDependencyTests
{
    [Theory][InlineData("runtime")][InlineData("native")][InlineData("runtimeTargets")]
    public void MatchingLeafInAnotherDirectoryDoesNotSatisfyAsset(string kind)
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Directory.CreateDirectory(Path.Combine(f.Install,"wrong"));File.WriteAllText(Path.Combine(f.Install,"wrong","asset.dat"),"synthetic");
        File.WriteAllText(Path.Combine(f.Install,"app.deps.json"),"{\"targets\":{\"net10.0\":{\"app/1.0\":{\""+kind+"\":{\"asset.dat\":{}}}}}}");
        Assert.Equal("DEPLOYMENT-DEPENDENCY-MISSING",Assert.Throws<InvalidDataException>(()=>Bind(f.Install,key)).Message);
    }
    [Fact] public void ExternalProbingRequiresReviewEvenWhenSigned()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(Path.Combine(f.Install,"app.runtimeconfig.json"),"{\"runtimeOptions\":{\"additionalProbingPaths\":[\"C:/external\"]}}");
        Assert.Equal("DEPLOYMENT-EXTERNAL-PROBING",Assert.Throws<InvalidDataException>(()=>Bind(f.Install,key)).Message);
    }
    private static SignedDeploymentClosure Bind(string root,ECDsa key)
    {
        var files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).Select(p=>new DeploymentInputFile(Path.GetRelativePath(root,p).Replace('\\','/'),new FileInfo(p).Length,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))),false)).ToArray();
        var raw=JsonSerializer.SerializeToUtf8Bytes(new DeploymentInputManifest("pccare-test-deployment-v1","1.0.0",new string('A',40),files));
        var publicKey=key.ExportSubjectPublicKeyInfo();return SignedDeploymentClosure.Bind(root,raw,key.SignData(raw,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation),publicKey,Convert.ToHexString(SHA256.HashData(publicKey)),false);
    }
}

public sealed class WO26UpdateUiContractTests
{
    [Fact] public void ProductRecheckAndPrivacyCopyCallTheImplementedService()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!Directory.Exists(Path.Combine(root.FullName,"src")))root=root.Parent;
        Assert.NotNull(root);
        var ui=File.ReadAllText(Path.Combine(root!.FullName,"src/SmartPerformanceDoctor.App/UpdateRecoveryWindow.cs"));
        Assert.Contains("UpdateRecoveryReconcile",ui);Assert.Contains("Task.Run(UpdateRecoveryCoordinator.Reconcile)",ui);
        Assert.Contains("UpdateDiagnosticExport.Serialize(observation)",ui);
        var helper=File.ReadAllText(Path.Combine(root.FullName,"src/SmartPerformanceDoctor.UpdateHelper/Program.cs"));
        Assert.Contains("coordinator.ResumeVerifiedRecovery(installRoot)",helper);
        Assert.DoesNotContain("coordinator.Recover(installRoot)",helper);
    }
}

public sealed class WO26StartupTrustRegressionTests
{
    [Fact] public void JournalPathOutsideSignedScopeReturnsReviewInsteadOfThrowing()
    {
        using var f=new WO23LegacyPublicationGapTests.Fixture();
        Assert.Equal(UpdateStage.Committed,f.Engine.Apply(f.Package("51.0.4","51.0.5","known.bin"),f.Install,"51.0.4").Stage);
        var record=Directory.GetFiles(f.Work,"journal.json",SearchOption.AllDirectories).Single();
        var journal=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(record))!;journal.Files[0].Path="payload/substituted.bin";
        File.WriteAllText(record,JsonSerializer.Serialize(journal));
        var observed=f.Engine.InspectInstallationHistory(f.Install);Assert.False(observed.Verified);
        Assert.True(f.Engine.InspectStartupTrust(f.Install).RequiresReview);
    }
}
