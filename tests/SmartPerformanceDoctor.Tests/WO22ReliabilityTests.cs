using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;
namespace SmartPerformanceDoctor.Tests;

public sealed class WO22ApprovalLifecycleTests
{
    internal static (SignedUpdateTests.Fixture F, LegacyRecoveryPlan Plan, string Request, string Hash) Create(bool enrolled=false)
    {
        var f=new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        if(enrolled)Assert.True(f.Transaction.EnrollInstallation(f.Install,f.Package("original-version"),"51.0.3",true).Verified);
        Assert.Equal(UpdateStage.Committed,f.Transaction.Apply(f.Package(),f.Install,"51.0.4").Stage);
        var path=Directory.GetFiles(f.Work,"journal.json",SearchOption.AllDirectories).Single();
        var journal=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
        journal.SchemaVersion=1;journal.Stage=UpdateStage.Applying;journal.InstallIdentity=journal.TransactionIdentity="";
        foreach(var e in journal.Files)e.OriginalIdentity=e.BackupIdentity=e.InstalledIdentity="";
        File.WriteAllText(path,JsonSerializer.Serialize(journal));
        var package=f.Package("original-version");var plan=f.Transaction.PlanLegacyRecovery(f.Install,journal.TransactionId,package,"51.0.3");
        var request=Path.Combine(Path.GetDirectoryName(f.Install)!,"request.json");
        File.WriteAllText(request,JsonSerializer.Serialize(new LegacyRestoreRequest(plan,package,"51.0.3"){Approval=new(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,true)}));
        return(f,plan,request,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(request))));
    }
    [Fact] public void UnreservedRequestDoesNotProveMutationOrConsent()
    {
        var(f,_,r,h)=Create();using(f){var result=f.Transaction.InspectLegacyApproval(f.Install,r,h);
        Assert.Equal(LegacyApprovalState.ApprovalRequested,result.State);Assert.False(result.FilesVerified);Assert.Null(result.CallerAcknowledged);}
    }
    [Theory]
    [InlineData(LegacyRestoreFaultPoint.RequestVerified,false,false)]
    [InlineData(LegacyRestoreFaultPoint.BeforeOperatorConfirmation,false,false)]
    [InlineData(LegacyRestoreFaultPoint.AfterOperatorConfirmation,false,false)]
    [InlineData(LegacyRestoreFaultPoint.ApprovalCreated,true,false)]
    [InlineData(LegacyRestoreFaultPoint.ApprovalFlushed,true,false)]
    [InlineData(LegacyRestoreFaultPoint.BeforePromotion,true,false)]
    [InlineData(LegacyRestoreFaultPoint.AfterPromotion,true,false)]
    [InlineData(LegacyRestoreFaultPoint.AfterFileRestore,true,false)]
    [InlineData(LegacyRestoreFaultPoint.AfterLastFileRestore,true,false)]
    [InlineData(LegacyRestoreFaultPoint.BeforeRestoredJournal,true,false)]
    [InlineData(LegacyRestoreFaultPoint.AfterRestoredJournal,true,true)]
    [InlineData(LegacyRestoreFaultPoint.BeforeResultReturn,true,true)]
    public void EveryCheckpointIsReadOnlyAndRequiresJournalAndBytes(LegacyRestoreFaultPoint point,bool consumed,bool committed)
    {
        var(f,p,r,h)=Create();using(f){
        try{f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true,x=>{if(x==point)throw new IOException("injected");});}catch(IOException){}
        var before=Directory.GetFiles(f.Work,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))));
        var result=new UpdateTransaction(f.Policy,f.Work,false).InspectLegacyApproval(f.Install,r,h);
        Assert.Equal(consumed,result.ApprovalConsumed);Assert.Equal(committed,result.FilesVerified);
        Assert.Equal(committed,result.State==LegacyApprovalState.RestorationCommitted);
        Assert.Null(result.CallerAcknowledged);
        Assert.All(before,x=>Assert.Equal(x.Value,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x.Key)))));
        if(consumed)Assert.Throws<IOException>(()=>f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true));
        }
    }
    [Fact] public void CompletedJournalWithChangedBytesIsNotSuccess()
    {
        var(f,_,r,h)=Create();using(f){Assert.Equal(UpdateStage.Restored,f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true).Stage);
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"changed after restoration");
        var result=f.Transaction.InspectLegacyApproval(f.Install,r,h);Assert.False(result.FilesVerified);Assert.Equal(LegacyApprovalState.RecoveryReviewRequired,result.State);}
    }
    [Fact] public void CompletedApprovalReplayCannotStartAnotherRestore()
    {
        var(f,_,r,h)=Create();using(f){f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true);
        Assert.Throws<IOException>(()=>f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true));
        Assert.True(f.Transaction.InspectLegacyApproval(f.Install,r,h).FilesVerified);}
    }
    [Fact] public void EmptyOrCorruptedReservationNeverProvesRestoration()
    {
        var(f,_,r,h)=Create();using(f){f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true);
        File.WriteAllText(Directory.GetFiles(f.Work,"legacy-approval-*.json").Single(),"{");
        Assert.Equal(LegacyApprovalState.RecoveryReviewRequired,f.Transaction.InspectLegacyApproval(f.Install,r,h).State);}
    }
}

public sealed class WO22CancellationSafetyTests
{
    [Theory]
    [InlineData(UpdateFaultPoint.AfterPackageVerification)]
    [InlineData(UpdateFaultPoint.AfterStagingCreated)]
    [InlineData(UpdateFaultPoint.DuringStageExtraction)]
    [InlineData(UpdateFaultPoint.DuringBackupCreation)]
    [InlineData(UpdateFaultPoint.AfterBackupVerification)]
    [InlineData(UpdateFaultPoint.AfterApplyingJournal)]
    [InlineData(UpdateFaultPoint.BeforeReplacement)]
    public void PreparationCancellationPreservesExistingFiles(UpdateFaultPoint point)
    {
        using var f=new SignedUpdateTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");
        using var cancel=new CancellationTokenSource();
        var result=f.Transaction.Apply(f.Package(),f.Install,"51.0.4",faultInjector:p=>{if(p==point)cancel.Cancel();},cancellationToken:cancel.Token);
        Assert.Equal("CANCELLED-PREPARATION",result.Code);Assert.True(result.WasCancellationRequested);Assert.False(result.AnyMutationStarted);
        Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));Assert.False(File.Exists(Path.Combine(f.Install,"second.txt")));
    }
    [Theory][InlineData(UpdateFaultPoint.AfterFirstReplacement)][InlineData(UpdateFaultPoint.BeforeCommittedJournal)]
    public void MutationCancellationDefersUntilVerifiedCommit(UpdateFaultPoint point)
    {
        using var f=new SignedUpdateTests.Fixture();using var c=new CancellationTokenSource();
        var result=f.Transaction.Apply(f.Package(),f.Install,"51.0.4",faultInjector:p=>{if(p==point)c.Cancel();},cancellationToken:c.Token);
        Assert.Equal(UpdateStage.Committed,result.Stage);Assert.True(result.AnyMutationStarted);Assert.True(result.WasCancellationRequested);
        Assert.Equal("DeferredUntilSafeCompletion",result.CancellationDisposition);Assert.True(f.Transaction.InspectInstallation(f.Install).Verified);
    }
    [Fact] public void RecoveryCancellationAfterStartCompletesVerifiedRestoration()
    {
        using var f=new SignedUpdateTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");
        var path=f.Package();using var c=new CancellationTokenSource();
        var result=f.Transaction.Apply(path,f.Install,"51.0.4",afterFileApplied:_=>throw new IOException("failure"),faultInjector:p=>{if(p==UpdateFaultPoint.AfterRollbackStarted)c.Cancel();},cancellationToken:c.Token);
        Assert.Equal(UpdateStage.Restored,result.Stage);Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
        Assert.True(result.WasCancellationRequested);Assert.True(result.AnyMutationStarted);
    }
}

public sealed class WO22LegacyContentReviewTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void UnknownCurrentContentAndMissingBackupRemainReadOnly(bool removeBackup)
    {
        var(f,p,_,_)=WO22ApprovalLifecycleTests.Create();using(f){var target=Path.Combine(f.Install,"app.txt");File.WriteAllText(target,"unknown bytes");
        if(removeBackup)File.Delete(Path.Combine(f.Work,"tx-"+p.TransactionId,"backup","app.txt"));
        var before=File.ReadAllBytes(target);var result=f.Transaction.ReviewLegacyContent(f.Install,p.TransactionId,f.Package("original-version"),"51.0.3");
        Assert.False(result.CanExecute);Assert.Equal("ManualReviewRequired",result.Result);
        Assert.Contains(result.Targets,t=>t.Result==(removeBackup?"BackupUnavailable":"BackupVerifiedButCurrentUnknown"));Assert.Equal(before,File.ReadAllBytes(target));}
    }
    [Fact] public void ReplacedIdentityIsReportedSeparately()
    {
        var(f,p,_,_)=WO22ApprovalLifecycleTests.Create();using(f){var target=Path.Combine(f.Install,"app.txt");File.Move(target,target+".retained");File.WriteAllText(target,"new");
        var result=f.Transaction.ReviewLegacyContent(f.Install,p.TransactionId,f.Package("original-version"),"51.0.3",p);
        Assert.Contains(result.Targets,t=>t.Result=="IdentityChanged");Assert.False(result.CanExecute);}
    }
}

public sealed class WO22InstallationTrustConsistencyTests
{
    [Fact] public void SignedEnrollmentIsRecheckedAfterLegacyRestoration()
    {
        var(f,_,r,h)=WO22ApprovalLifecycleTests.Create(true);using(f){
        Assert.Equal(UpdateStage.Restored,f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true).Stage);
        var result=f.Transaction.InspectInstallationTrust(f.Install);
        Assert.True(result.Verified);Assert.Equal("SIGNED-ENROLLMENT-VERIFIED",result.Code);}
    }
    [Theory][InlineData("content","INSTALLATION-CONTENT-MISMATCH")][InlineData("identity","INSTALLATION-IDENTITY-MISMATCH")]
    public void InstallationMismatchCodesIdentifyTheFailure(string kind,string expected)
    {
        using var f=new SignedUpdateTests.Fixture();f.Transaction.Apply(f.Package(),f.Install,"51.0.4");var path=Path.Combine(f.Install,"app.txt");
        if(kind=="content")File.WriteAllText(path,"changed");else{File.Move(path,path+".retained");File.WriteAllText(path,"new");}
        Assert.Equal(expected,f.Transaction.InspectInstallationTrust(f.Install).Code);
    }
    [Theory][InlineData(EnrollmentFaultPoint.PreparedRecordCreated)][InlineData(EnrollmentFaultPoint.RecordFlushed)][InlineData(EnrollmentFaultPoint.BeforeActivation)]
    public void InterruptedEnrollmentPreservesPayloadAndCannotBecomeVerified(EnrollmentFaultPoint point)
    {
        using var f=new SignedUpdateTests.Fixture();File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");File.WriteAllText(Path.Combine(f.Install,"second.txt"),"old-second");
        Assert.Throws<IOException>(()=>f.Transaction.EnrollInstallation(f.Install,f.Package("original-version"),"51.0.3",true,p=>{if(p==point)throw new IOException("injected");}));
        var result=f.Transaction.InspectInstallationTrust(f.Install);Assert.False(result.Verified);Assert.True(result.RequiresReview);
        Assert.Equal("old",File.ReadAllText(Path.Combine(f.Install,"app.txt")));Assert.Equal("ENROLLMENT-PREPARATION-INCOMPLETE",result.Code);
    }
    [Fact] public void RestoredMarkerCannotHideSubsequentFileChanges()
    {
        var(f,_,r,h)=WO22ApprovalLifecycleTests.Create();using(f){f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true);
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"unexpected");
        Assert.Equal(UpdateRecoveryState.RecoveryRequired,f.Transaction.InspectRecovery(f.Install).State);}
    }
    [Fact] public void ExtraUnlistedDataIsPreservedButNotAttested()
    {
        using var f=new SignedUpdateTests.Fixture();f.Transaction.Apply(f.Package(),f.Install,"51.0.4");
        File.WriteAllText(Path.Combine(f.Install,"user.data"),"unlisted");Assert.True(f.Transaction.InspectInstallation(f.Install).Verified);
        Assert.Equal("unlisted",File.ReadAllText(Path.Combine(f.Install,"user.data")));
    }
}

public sealed class WO22ApprovalReplayTests
{
    [Fact] public async Task ConcurrentDuplicateApprovalCannotReserveTwice()
    {
        var(f,_,r,h)=WO22ApprovalLifecycleTests.Create();using(f){using var ready=new CountdownEvent(2);
        Task<bool> Attempt()=>Task.Run(()=>{try{return f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>{ready.Signal();if(!ready.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException();return true;}).Stage==UpdateStage.Restored;}catch(IOException){return false;}});
        var results=await Task.WhenAll(Attempt(),Attempt());Assert.Single(results,x=>x);
        Assert.Single(Directory.GetFiles(f.Work,"legacy-approval-*.json"));Assert.True(f.Transaction.InspectLegacyApproval(f.Install,r,h).FilesVerified);}
    }
}
public sealed class WO22TrustFailureTests
{
    [Fact] public void CorruptCurrentPointerDoesNotSilentlySelectPreviousRecord()
    {
        using var f=new SignedUpdateTests.Fixture();f.Transaction.Apply(f.Package(),f.Install,"51.0.4");f.Transaction.Apply(f.Package(),f.Install,"51.0.4");
        var current=Path.Combine(f.Work,"installation-current.json");File.WriteAllText(current,"{");
        Assert.True(File.Exists(Path.Combine(f.Work,"installation-previous.json")));
        var decision=f.Transaction.InspectInstallationTrust(f.Install);Assert.False(decision.Verified);Assert.True(decision.RequiresReview);
        Assert.Equal("{",File.ReadAllText(current));
    }
    [Fact] public void SignedDeclarationCannotBeReplacedByForgedSignatureAndMatchingChecksum()
    {
        using var f=new SignedUpdateTests.Fixture();f.Transaction.Apply(f.Package(),f.Install,"51.0.4");
        var current=Path.Combine(f.Work,"installation-current.json");var pointer=JsonSerializer.Deserialize<InstallationPointer>(File.ReadAllText(current))!;
        var record=Path.Combine(f.Work,"tx-"+pointer.TransactionId,"committed-attestation.json");var saved=JsonSerializer.Deserialize<CommittedInstallation>(File.ReadAllText(record))!;
        File.WriteAllText(record,JsonSerializer.Serialize(saved with {Signature=Convert.ToBase64String(new byte[64])}));
        File.WriteAllText(current,JsonSerializer.Serialize(pointer with {AttestationHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(record)))}));
        var decision=f.Transaction.InspectInstallationTrust(f.Install);Assert.False(decision.Verified);Assert.Equal("INSTALLATION-SIGNATURE-REJECTED",decision.Code);
    }
    [Fact] public void ChangedSecurityIsReportedByReadOnlyLegacyReview()
    {
        var(f,p,_,_)=WO22ApprovalLifecycleTests.Create();using(f){var path=Path.Combine(f.Install,"app.txt");var info=new FileInfo(path);
        var acl=System.IO.FileSystemAclExtensions.GetAccessControl(info);
        acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid,null),System.Security.AccessControl.FileSystemRights.Read,System.Security.AccessControl.AccessControlType.Allow));
        System.IO.FileSystemAclExtensions.SetAccessControl(info,acl);
        var result=f.Transaction.ReviewLegacyContent(f.Install,p.TransactionId,f.Package("original-version"),"51.0.3",p);
        Assert.Contains(result.Targets,t=>t.Result=="SecurityChanged");Assert.False(result.CanExecute);}
    }
}

public sealed class WO22ConflictingRecoveryTests
{
    [Fact] public void CompanionPendingTransactionBlocksReadOnlyResumeAdvice()
    {
        var(f,plan,r,h)=WO22ApprovalLifecycleTests.Create();using(f){
        Assert.Throws<IOException>(()=>f.Transaction.ExecuteLegacyRecoveryRequest(f.Install,r,h,_=>true,p=>{if(p==LegacyRestoreFaultPoint.AuthorizationVerified)throw new IOException("injected");}));
        var id=Guid.NewGuid().ToString("N");var directory=Path.Combine(f.Work,"tx-"+id);Directory.CreateDirectory(directory);
        using(var pin=WindowsFileBoundary.PinDirectory(directory))
        File.WriteAllText(Path.Combine(directory,"journal.json"),JsonSerializer.Serialize(new UpdateJournal{TransactionId=id,InstallRoot=f.Install,InstallIdentity=plan.InstallIdentity,TransactionIdentity=pin.Identity,Stage=UpdateStage.Applying}));
        var query=f.Transaction.InspectLegacyApproval(f.Install,r,h);
        Assert.Equal(LegacyApprovalState.RecoveryReviewRequired,query.State);Assert.False(query.CanResumeRecovery);Assert.False(query.FilesVerified);
        var review=f.Transaction.ReviewLegacyContent(f.Install,plan.TransactionId,f.Package("original-version"),"51.0.3",plan);
        Assert.Equal("MultiplePendingTransactions",review.Result);Assert.False(review.CanExecute);
        }
    }
}
