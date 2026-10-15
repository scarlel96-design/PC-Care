using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO21QualificationTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void InstallationAclRejectsNonAdministratorMutation(bool writable)
    {
        var acl=new FileSecurity();
        var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
        var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
        var users=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
        acl.SetOwner(admins);acl.SetAccessRuleProtection(true,false);
        foreach(var sid in new[]{admins,system})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(users,writable?FileSystemRights.Modify:FileSystemRights.ReadAndExecute,AccessControlType.Allow));
        if(writable)Assert.Throws<UnauthorizedAccessException>(()=>UpdateTransaction.ValidateProtectedSecurity(acl,false));
        else UpdateTransaction.ValidateProtectedSecurity(acl,false);
    }
    private static (SignedUpdateTests.Fixture Fixture, LegacyRecoveryPlan Plan, string Package) Legacy()
    {
        var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");
        File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);
        var path=Directory.GetFiles(f.Work,"journal.json",SearchOption.AllDirectories).Single();
        var j=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
        j.SchemaVersion=1;j.Stage=UpdateStage.Applying;j.InstallIdentity=j.TransactionIdentity="";
        foreach(var e in j.Files)e.OriginalIdentity=e.BackupIdentity=e.InstalledIdentity="";
        File.WriteAllText(path,JsonSerializer.Serialize(j));
        var package=f.Package("original-version");
        return(f,f.Transaction.PlanLegacyRecovery(f.Install,j.TransactionId,package,"51.0.3"),package);
    }
    private static (string Path,string Hash) Request(SignedUpdateTests.Fixture f,LegacyRecoveryPlan p,string package,LegacyApprovalIntent? intent)
    {
        var path=Path.Combine(Path.GetDirectoryName(f.Install)!,"request.json");
        File.WriteAllText(path,JsonSerializer.Serialize(new LegacyRestoreRequest(p,package,"51.0.3"){Approval=intent}));
        return(path,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }
    [Theory]
    [InlineData(0,true,true)][InlineData(-599,true,true)][InlineData(-601,true,false)]
    [InlineData(1,true,false)][InlineData(0,false,false)]
    public void ApprovalTimeAndConsentAreRequired(int seconds,bool consent,bool accepted)
    {
        var now=DateTimeOffset.UtcNow;
        Assert.Equal(accepted,LegacyApprovalPolicy.IsCurrent(new(Guid.NewGuid().ToString("N"),now.AddSeconds(seconds),consent),now));
    }
    [Fact] public void MissingIntentCannotGrantAuthority()
    {Assert.False(LegacyApprovalPolicy.IsCurrent(null,DateTimeOffset.UtcNow));Assert.False(LegacyApprovalPolicy.IsCurrent(new("bad",DateTimeOffset.UtcNow,true),DateTimeOffset.UtcNow));}
    [Fact] public void ElevatedEntryRequiresItsOwnApproval()
    {
        var(f,p,package)=Legacy();using(f)
        {
            var strict=new UpdateTransaction(PublisherTrustPolicy.Production,f.Work,true);
            var result=strict.ExecuteLegacyRecovery(f.Install,p,package,"51.0.3",p.ConsentBinding,true);
            Assert.Equal("LEGACY-ELEVATED-APPROVAL-REQUIRED",result.Code);
            Assert.Equal("new",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Fact] public void ApprovedRequestRestoresAndRetainsScope()
    {
        var(f,p,package)=Legacy();using(f)
        {
            var intent=new LegacyApprovalIntent(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,true);
            var request=Request(f,p,package,intent);
            var result=f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,request.Path,request.Hash,_=>true);
            Assert.Equal(UpdateStage.Restored,result.Stage);
            var receipt=JsonSerializer.Deserialize<LegacyApprovalScope>(File.ReadAllText(Path.Combine(f.Work,"legacy-approval-"+intent.RequestId+".json")))!;
            Assert.Equal(request.Hash,receipt.RequestHash);Assert.Equal(p.TransactionId,receipt.TransactionId);
            Assert.Equal(p.PackageIdentity,receipt.PackageIdentity);Assert.Equal(p.Targets.Count,receipt.Targets.Count);
            Assert.NotEmpty(receipt.AdministratorSession);Assert.NotEmpty(receipt.AdministratorSid);
            Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Theory][InlineData("cancel")][InlineData("expired")][InlineData("hash")][InlineData("changed-target")]
    public void RejectedRequestPreservesTargets(string reason)
    {
        var(f,p,package)=Legacy();using(f)
        {
            var request=Request(f,p,package,new(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow.AddMinutes(reason=="expired"?-11:0),true));
            if(reason=="changed-target")File.WriteAllText(Path.Combine(f.Install,"app.txt"),"unrelated user content");
            var before=File.ReadAllBytes(Path.Combine(f.Install,"app.txt"));
            TransactionResult? result=null;
            try{result=f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,request.Path,reason=="hash"?new string('0',64):request.Hash,_=>reason!="cancel");}
            catch(InvalidDataException){}
            Assert.True(result is null||result.Stage==UpdateStage.RecoveryRequired);
            Assert.Equal(before,File.ReadAllBytes(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Fact] public void InterruptedApprovalCannotBeReused()
    {
        var(f,p,package)=Legacy();using(f)
        {
            var request=Request(f,p,package,new(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,true));
            Assert.Throws<IOException>(()=>f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,request.Path,request.Hash,_=>true,point=>
                {if(point==LegacyRestoreFaultPoint.AuthorizationVerified)throw new IOException("injected");}));
            Assert.Throws<IOException>(()=>f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,request.Path,request.Hash,_=>true));
            Assert.Equal("new",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Fact] public void UnattestedLegacyIsUnverifiedWithoutMalwareClassification()
    {
        using var f=new SignedUpdateTests.Fixture();
        Assert.Equal(InstallationTrustState.FreshInstallation,f.Transaction.InspectInstallationTrust(f.Install).State);
        File.WriteAllText(Path.Combine(f.Install,"existing.exe"),"legacy");
        var result=f.Transaction.InspectInstallationTrust(f.Install);
        Assert.Equal(InstallationTrustState.LegacyUnattestedInstallation,result.State);Assert.False(result.Verified);Assert.False(result.RequiresReview);
    }
    [Fact] public void SignedEnrollmentVerifiesBytesWithoutRewritingInstallation()
    {
        using var f=new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        var package=f.Package("original-version");
        Assert.True(f.Transaction.EnrollInstallation(f.Install,package,"51.0.3",true).Verified);
        Assert.True(f.Transaction.InspectInstallationTrust(f.Install).Verified);
        Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        var record=File.ReadAllBytes(Path.Combine(f.Work,"installation-enrollment.json"));
        Assert.False(f.Transaction.EnrollInstallation(f.Install,package,"51.0.3",true).Verified);
        Assert.Equal(record,File.ReadAllBytes(Path.Combine(f.Work,"installation-enrollment.json")));
    }
    [Theory][InlineData("content")][InlineData("identity")][InlineData("record")]
    public void EnrollmentMismatchIsVisible(string reason)
    {
        using var f=new SignedUpdateTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        Assert.True(f.Transaction.EnrollInstallation(f.Install,f.Package("original-version"),"51.0.3",true).Verified);
        var path=Path.Combine(f.Install,"app.txt");
        if(reason=="content")File.WriteAllText(path,"changed");
        if(reason=="identity"){File.Move(path,path+".old");File.WriteAllText(path,"old");}
        if(reason=="record")File.WriteAllText(Path.Combine(f.Work,"installation-enrollment.json"),"{");
        var result=f.Transaction.InspectInstallationTrust(f.Install);Assert.False(result.Verified);Assert.True(result.RequiresReview);
        Assert.Equal(reason=="record"?InstallationTrustState.AttestationCorrupted:InstallationTrustState.SignedInstallationMismatch,result.State);
    }
    [Fact] public void EnrollmentNeedsConsentAndProductionTrust()
    {
        using var f=new SignedUpdateTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");
        Assert.False(f.Transaction.EnrollInstallation(f.Install,f.Package(),"51.0.4",false).Verified);
        Assert.False(new UpdateTransaction(PublisherTrustPolicy.Production,f.Work,false).EnrollInstallation(f.Install,f.Package(),"51.0.4",true).Verified);
        Assert.False(File.Exists(Path.Combine(f.Work,"installation-enrollment.json")));
    }
    [Fact] public void DeletedAttestationWithHistoryIsNotLegacyEnrollment()
    {
        using var f=new SignedUpdateTests.Fixture();Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);
        File.Delete(Path.Combine(f.Work,"installation-current.json"));
        Assert.Equal(InstallationTrustState.AttestationMissing,f.Transaction.InspectInstallationTrust(f.Install).State);
    }
    [Fact] public void PendingTransactionIsNotAttested()
    {
        using var f=new SignedUpdateTests.Fixture();Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);
        var path=Directory.GetFiles(f.Work,"journal.json",SearchOption.AllDirectories).Single();var j=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
        j.Stage=UpdateStage.Applying;File.WriteAllText(path,JsonSerializer.Serialize(j));
        Assert.Equal(InstallationTrustState.PendingTransaction,f.Transaction.InspectInstallationTrust(f.Install).State);
    }
    [Fact] public void UnknownCurrentBytesNeedFreshReviewedScope()
    {
        var(f,p,package)=Legacy();using(f)
        {
            File.WriteAllText(Path.Combine(f.Install,"app.txt"),"partial unrelated content");
            var before=File.ReadAllBytes(Path.Combine(f.Install,"app.txt"));
            Assert.Equal(UpdateStage.RecoveryRequired,f.Transaction.ExecuteLegacyRecovery(f.Install,p,package,"51.0.3",p.ConsentBinding,true).Stage);
            Assert.Equal(before,File.ReadAllBytes(Path.Combine(f.Install,"app.txt")));
            var fresh=f.Transaction.PlanLegacyRecovery(f.Install,p.TransactionId,package,"51.0.3");
            Assert.NotEqual(p.ConsentBinding,fresh.ConsentBinding);
            Assert.All(fresh.Targets,t=>Assert.Equal("BackupVerified",t.Status));
            Assert.Contains(fresh.Targets,t=>t.CurrentContentState=="UnknownContent");
            Assert.Equal(UpdateStage.RecoveryRequired,f.Transaction.ExecuteLegacyRecovery(f.Install,fresh,package,"51.0.3",fresh.ConsentBinding,true).Stage);
            Assert.Equal(before,File.ReadAllBytes(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Fact] public void TargetChangeInsideConfirmationIsRejected()
    {
        var(f,p,package)=Legacy();using(f)
        {
            var request=Request(f,p,package,new(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,true));
            var result=f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,request.Path,request.Hash,_=>
            {File.WriteAllText(Path.Combine(f.Install,"app.txt"),"changed during approval");return true;});
            Assert.Equal(UpdateStage.RecoveryRequired,result.Stage);
            Assert.Equal("changed during approval",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Fact] public void DuplicateJsonPropertiesAreRejected()
    {
        var(f,p,package)=Legacy();using(f)
        {
            var request=Request(f,p,package,new(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,true));
            var text=File.ReadAllText(request.Path);File.WriteAllText(request.Path,text.Insert(1,"\"Plan\":null,"));
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(request.Path)));
            Assert.Throws<InvalidDataException>(()=>f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,request.Path,hash,_=>true));
            Assert.Equal("new",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        }
    }
    [Fact] public void MissingSignedCurrentDeclarationCannotAuthenticateUpdatedBytes()
    {
        var(f,p,package)=Legacy();using(f)
        {
            File.Delete(Directory.GetFiles(f.Work,"committed-attestation.json",SearchOption.AllDirectories).Single());
            var fresh=f.Transaction.PlanLegacyRecovery(f.Install,p.TransactionId,package,"51.0.3");
            Assert.All(fresh.Targets,t=>Assert.Equal("UnknownContent",t.CurrentContentState));
            Assert.Equal(UpdateStage.RecoveryRequired,f.Transaction.ExecuteLegacyRecovery(f.Install,fresh,package,"51.0.3",fresh.ConsentBinding,true).Stage);
        }
    }
    [Fact] public void TruncatedEnrollmentNeverBecomesVerified()
    {
        using var f=new SignedUpdateTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        Assert.True(f.Transaction.EnrollInstallation(f.Install,f.Package("original-version"),"51.0.3",true).Verified);
        File.WriteAllText(Path.Combine(f.Work,"installation-enrollment.json"),"null");
        Assert.True(f.Transaction.InspectInstallationTrust(f.Install).RequiresReview);
    }
}
