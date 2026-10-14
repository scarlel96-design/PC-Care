using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;

namespace SmartPerformanceDoctor.Tests;
public sealed class WO20RecoveryTests
{
    private static (SignedUpdateTests.Fixture Fixture,LegacyRecoveryPlan Plan,string Package,string Journal) Prepare()
    {
        WO20TestEnvironment.Validate(Environment.GetEnvironmentVariable("RUNNER_TEMP"));
        var f=new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);
        var path=Directory.GetFiles(f.Work,"journal.json",SearchOption.AllDirectories).Single();
        var journal=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
        journal.SchemaVersion=1;journal.Stage=UpdateStage.Applying;journal.InstallIdentity=journal.TransactionIdentity="";
        foreach(var entry in journal.Files)entry.OriginalIdentity=entry.BackupIdentity=entry.InstalledIdentity="";
        File.WriteAllText(path,JsonSerializer.Serialize(journal));
        var package=f.Package("original-version");var plan=f.Transaction.PlanLegacyRecovery(f.Install,journal.TransactionId,package,"51.0.3");
        return(f,plan,package,path);
    }
    [Fact] public void ReviewedLegacyRestoreUsesExistingRollbackEngine()
    {
        var (f,plan,package,journal)=Prepare();using(f)
        {
            Assert.False(plan.CanExecute);
            var result=f.Transaction.ExecuteLegacyRecovery(f.Install,plan,package,"51.0.3",plan.ConsentBinding,true);
            Assert.Equal(UpdateStage.Restored,result.Stage);Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
            Assert.Equal("old-second",File.ReadAllText(Path.Combine(f.Install,"second.txt")));
            var saved=Directory.GetFiles(Path.GetDirectoryName(journal)!,"legacy-original-*.json").Single();
            Assert.Equal(plan.JournalHash,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(saved))));
        }
    }
    [Theory][InlineData("consent")][InlineData("binding")][InlineData("target")][InlineData("backup")][InlineData("journal")][InlineData("package")][InlineData("publisher")][InlineData("identity")][InlineData("package-identity")][InlineData("plan-tamper")]
    public void ChangedAuthorizationEvidenceBlocksBeforeTargetMutation(string change)
    {
        var(f,plan,package,journal)=Prepare();using(f)
        {
            if(change=="target")File.WriteAllText(Path.Combine(f.Install,"app.txt"),"user-modified");
            if(change=="backup")File.WriteAllText(Path.Combine(Path.GetDirectoryName(journal)!,"backup/app.txt"),"tampered");
            if(change=="journal")File.AppendAllText(journal," ");
            if(change=="package")File.AppendAllText(package,"modified");
            if(change=="package-identity"){File.Move(package,package+".original");File.Copy(package+".original",package);}
            if(change=="plan-tamper")plan=plan with {Targets=plan.Targets.Select(t=>t with {CurrentHash=new string('0',64)}).ToArray()};
            if(change=="identity"){var target=Path.Combine(f.Install,"app.txt");File.Move(target,target+".original");File.WriteAllText(target,"new");}
            var before=File.ReadAllBytes(Path.Combine(f.Install,"app.txt"));
            var transaction=change=="publisher"?new UpdateTransaction(PublisherTrustPolicy.Production,f.Work,false):f.Transaction;
            TransactionResult? result=null;
            try{result=transaction.ExecuteLegacyRecovery(f.Install,plan,package,"51.0.3",change=="binding"?"bad":plan.ConsentBinding,change!="consent");}
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){}
            Assert.True(result is null||result.Stage==UpdateStage.RecoveryRequired);
            Assert.Equal(before,File.ReadAllBytes(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Fact] public void TargetChangeImmediatelyBeforePromotionDoesNotOverwriteUserBytes()
    {
        var(f,plan,package,journal)=Prepare();using(f)
        {
            Assert.Throws<InvalidDataException>(()=>f.Transaction.ExecuteLegacyRecovery(f.Install,plan,package,"51.0.3",plan.ConsentBinding,true,p=>
            {if(p==LegacyRestoreFaultPoint.AuthorizationVerified)File.WriteAllText(Path.Combine(f.Install,"app.txt"),"later-user-content");}));
            Assert.Equal("later-user-content",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
            Assert.Equal(plan.JournalHash,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(journal))));
        }
    }
    [Theory][InlineData(LegacyRestoreFaultPoint.AuthorizationVerified)][InlineData(LegacyRestoreFaultPoint.BeforePromotion)]
    [InlineData(LegacyRestoreFaultPoint.AfterPromotion)][InlineData(LegacyRestoreFaultPoint.BeforeRestore)]
    [InlineData(LegacyRestoreFaultPoint.AfterFileRestore)][InlineData(LegacyRestoreFaultPoint.BeforeRestoredJournal)][InlineData(LegacyRestoreFaultPoint.AfterRestoredJournal)]
    public void InterruptedLegacyRestorePreservesApprovedBytes(LegacyRestoreFaultPoint point)
    {
        var(f,plan,package,journal)=Prepare();using(f)
        {
            var fired=false;
            try{f.Transaction.ExecuteLegacyRecovery(f.Install,plan,package,"51.0.3",plan.ConsentBinding,true,p=>{if(p==point&&!fired){fired=true;throw new IOException("WO20 synthetic fault");}});}catch(IOException){}
            Assert.True(fired);
            if(point is LegacyRestoreFaultPoint.AuthorizationVerified or LegacyRestoreFaultPoint.BeforePromotion)
            {
                Assert.Equal("new",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
                Assert.Equal(UpdateStage.Restored,f.Transaction.ExecuteLegacyRecovery(f.Install,plan,package,"51.0.3",plan.ConsentBinding,true).Stage);
            }
            else Assert.Equal(UpdateStage.Restored,f.Transaction.Recover(f.Install).Stage);
            Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
            Assert.Equal("old-second",File.ReadAllText(Path.Combine(f.Install,"second.txt")));
        }
    }
    [Theory][InlineData(false,false,false,false,BootstrapTransitionState.SignedPackageRejected)]
    [InlineData(true,false,false,true,BootstrapTransitionState.BootstrapNotRequired)]
    [InlineData(false,false,false,true,BootstrapTransitionState.BridgeUnavailable)]
    [InlineData(false,true,false,true,BootstrapTransitionState.BridgeUntrusted)]
    [InlineData(false,true,true,true,BootstrapTransitionState.BootstrapTransitionRequired)]
    public void BootstrapDispatchSeparatesTrustAndProtocol(bool protocol,bool present,bool trusted,bool package,BootstrapTransitionState expected)
    {Assert.Equal(expected,BootstrapDispatch.Describe(protocol,present,trusted,package).State);Assert.False(BootstrapDispatch.Describe(protocol,present,trusted,package).ProcessLaunched);}
    [Fact] public void ProductionRejectsUnsignedBridgeAndFixturePackage()
    {
        using var f=new SignedUpdateTests.Fixture();
        var signature=AuthenticodeBoundary.Verify(Path.Combine(f.Install,"missing.exe"));Assert.False(AuthenticodeBoundary.IsBuildPublisher(signature));
        Assert.False(SignedPackage.Verify(f.Package(),"51.0.4",PublisherTrustPolicy.Production).CanApply);
        Assert.Empty(BuildPublisherTrust.ExecutableThumbprints);
    }
    [Theory][InlineData("content")][InlineData("identity")][InlineData("declaration")]
    public void StartupAttestationDetectsInstallationChanges(string change)
    {
        using var f=new SignedUpdateTests.Fixture();Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);
        Assert.True(f.Transaction.InspectInstallation(f.Install).Verified);
        if(change=="content")File.WriteAllText(Path.Combine(f.Install,"app.txt"),"tampered");
        if(change=="identity"){var p=Path.Combine(f.Install,"app.txt");File.Move(p,p+".old");File.WriteAllText(p,"new");}
        if(change=="declaration")File.WriteAllText(Directory.GetFiles(f.Work,"committed-attestation.json",SearchOption.AllDirectories).Single(),"{}");
        var result=f.Transaction.InspectInstallation(f.Install);Assert.False(result.Verified);Assert.True(result.RequiresReview);
    }
    [Fact] public void NoAttestationNeverReturnsVerified()
    {using var f=new SignedUpdateTests.Fixture();Assert.False(f.Transaction.InspectInstallation(f.Install).Verified);}
    [Fact] public void BoundedFolderScannerReportsDirectoryLimitWithoutThreadPoolFanout()
    {
        var parent=WO20TestEnvironment.Validate(Environment.GetEnvironmentVariable("RUNNER_TEMP"));
        var root=Path.Combine(parent,"wo20-scan-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            for(var i=0;i<80;i++)Directory.CreateDirectory(Path.Combine(root,i.ToString()));
            var result=SmartPerformanceDoctor.App.Services.SystemCare.CareFolderScanner.Measure(root,default,maxFiles:1);
            Assert.Equal("file_cap",result.Note);Assert.Equal(0,result.FileCount);
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(()=>SmartPerformanceDoctor.App.Services.SystemCare.CareFolderScanner.Measure(root,cancelled.Token));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact] public void MissingSyntheticRootProducesExplicitError()
    {Assert.Throws<InvalidOperationException>(()=>WO20TestEnvironment.Validate(null));Assert.Throws<InvalidOperationException>(()=>WO20TestEnvironment.Validate(AppContext.BaseDirectory));}
}
public static class WO20TestEnvironment
{
    public static string Validate(string? root)
    {
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root))throw new InvalidOperationException("WO20_TEST_ROOT_REQUIRED");
        var full=Path.GetFullPath(root).TrimEnd('\\');
        foreach(var protectedRoot in new[]{AppContext.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)})
            if(!string.IsNullOrWhiteSpace(protectedRoot)&&full.StartsWith(Path.GetFullPath(protectedRoot).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("WO20_TEST_ROOT_PROTECTED");
        if(!Directory.Exists(full))throw new InvalidOperationException("WO20_TEST_ROOT_MISSING");
        using var pin=WindowsFileBoundary.PinDirectory(full);return full;
    }
}
