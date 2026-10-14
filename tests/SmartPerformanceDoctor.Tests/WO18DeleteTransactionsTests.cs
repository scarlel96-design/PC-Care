using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO18DeleteTransactionsTests : IDisposable
{
    private readonly string root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")
        ?? throw new InvalidOperationException("ExplicitSyntheticParentRequired"), "wo18-" + Guid.NewGuid().ToString("N"));
    public WO18DeleteTransactionsTests() => Directory.CreateDirectory(root);
    private string Fixture(int length = 65537)
    {
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(length)); return path;
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private ProfessionalSecureDeleteService Service() => new(root);
    private string Audit => Path.Combine(root, "audit");

    [Fact] public async Task CompletedAuditUsesFinalHandleIdentityAndHash()
    {
        var path = Fixture(); var hash = Hash(path);
        string identity; using (var file = WindowsFileBoundary.OpenRead(root, Path.GetFileName(path))) identity = file.Identity;
        var service = Service(); var plan = service.PlanDryRun([path]);
        var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다");
        Assert.Equal("Completed", result.Status); Assert.Equal(1, result.Deleted); Assert.False(result.Items[0].PartialWritePossible);
        Assert.True(result.AnyMutationStarted); Assert.False(result.RequiresManualReview);
        var key = SecureDeleteAuditChain.LoadOperationKey(plan.OperationId, Audit);
        try
        {
            var records = SecureDeleteJournal.Read(Audit, plan.OperationId, key);
            var bound = Assert.Single(records, r => r.State == "TargetHandleBound");
            Assert.Equal(identity, bound.FileIdentity);
            Assert.Equal(SecureDeleteAuditChain.ProtectContentFingerprint(hash, key), bound.ContentFingerprint);
            var audit = File.ReadAllText(result.AuditPath);
            Assert.Contains(identity, audit); Assert.Contains(bound.ContentFingerprint, audit);
            Assert.DoesNotContain(path, File.ReadAllText(result.JournalPath)); Assert.DoesNotContain(hash, audit);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
        var reconciled = SecureDeleteJournal.Reconcile(Audit, plan.OperationId);
        Assert.False(reconciled.RequiresManualReview); Assert.Equal("DeletionConfirmedByAvailableEvidence", reconciled.Status);
    }

    [Theory]
    [InlineData("JournalBeforeWrite:DestructiveWriteIntent")]
    [InlineData("JournalBeforeFlush:DestructiveWriteIntent")]
    public async Task IntentRecordFailurePreventsMutation(string point)
    {
        var path = Fixture(); var before = Hash(path); var service = Service();
        service.TestCheckpoint = p => { if (p == point) throw new IOException("InjectedJournalFailure"); };
        var plan = service.PlanDryRun([path]); var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다");
        Assert.Equal(before, Hash(path)); Assert.NotEqual("Completed", result.Status);
        Assert.True(SecureDeleteJournal.Reconcile(Audit, plan.OperationId).RequiresManualReview);
    }

    [Fact] public async Task CancellationBeforeMutationIsIndependentFromPartialCompletion()
    {
        var path = Fixture(); var before = Hash(path); var service = Service(); var plan = service.PlanDryRun([path]);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다", cancellationToken: cts.Token);
        Assert.Equal("Cancelled", result.Status); Assert.True(result.WasCancellationRequested);
        Assert.False(result.AnyMutationStarted); Assert.False(result.Items[0].PartialWritePossible);
        Assert.Equal(before, Hash(path)); Assert.Equal(1, result.IncompleteTargets);
    }

    [Fact] public async Task CancellationDuringWriteNeverRepeatsDestruction()
    {
        var path = Fixture(1048576); var other = Fixture(); var otherHash = Hash(other);
        var service = Service(); using var cts = new CancellationTokenSource();
        service.TestCheckpoint = p => { if (p == "DuringOverwrite") cts.Cancel(); };
        var plan = service.PlanDryRun([path]);
        var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다", cancellationToken: cts.Token);
        Assert.True(result.WasCancellationRequested); Assert.True(result.AnyMutationStarted);
        Assert.True(result.RequiresManualReview); Assert.True(result.Items[0].PartialWritePossible);
        var after = Hash(path); var reconciliation = SecureDeleteJournal.Reconcile(Audit, plan.OperationId);
        Assert.Contains(reconciliation.Items, r => r.Observation == "PartialWriteObserved");
        Assert.Equal(after, Hash(path)); Assert.Equal(otherHash, Hash(other));
        service.TestCheckpoint = null;
        var second = await service.ApplyDetailedAsync(service.PlanDryRun([path]), "보안 삭제에 동의합니다");
        Assert.NotEqual("Completed", second.Status); Assert.Equal(after, Hash(path));
    }

    [Fact] public async Task CancellationAfterOneDeletedTargetRetainsCompletedAndIncompleteCounts()
    {
        var first=Fixture();var second=Fixture();var secondHash=Hash(second);
        var service=Service();using var cts=new CancellationTokenSource();
        service.TestCheckpoint=p=>{if(p=="AfterAudit")cts.Cancel();};
        var plan=service.PlanDryRun([first,second]);
        var result=await service.ApplyDetailedAsync(plan,"보안 삭제에 동의합니다",cancellationToken:cts.Token);
        Assert.True(result.WasCancellationRequested);Assert.True(result.AnyMutationStarted);
        Assert.Equal(1,result.Deleted);Assert.Equal(1,result.CompletedTargets);Assert.Equal(1,result.IncompleteTargets);
        Assert.False(result.Items[0].PartialWritePossible);Assert.Equal(secondHash,Hash(second));
    }

    [Theory][InlineData("BeforeAudit")][InlineData("AfterAudit")][InlineData("BeforeResultJson")][InlineData("AfterResultJson")]
    public async Task PostDeletionRecordFailureIsVisible(string point)
    {
        var path = Fixture(); var service = Service(); var plan = service.PlanDryRun([path]);
        service.TestCheckpoint = p => { if (p == point) throw new IOException("InjectedRecordFailure"); };
        var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다");
        Assert.False(File.Exists(path)); Assert.Equal("RecoveryReviewRequired", result.Status);
        Assert.True(result.RequiresManualReview); Assert.True(SecureDeleteJournal.Reconcile(Audit, plan.OperationId).RequiresManualReview);
    }

    [Theory][InlineData("empty")][InlineData("truncate")][InlineData("tamper")][InlineData("missing-key")][InlineData("result-tamper")]
    public async Task UnauthenticatedOrIncompleteEvidenceCannotCertifyDeletion(string damage)
    {
        var path = Fixture(); var service = Service(); var plan = service.PlanDryRun([path]);
        var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다");
        if (damage == "empty") File.WriteAllText(result.JournalPath, "");
        if (damage == "truncate") { var data = File.ReadAllBytes(result.JournalPath); File.WriteAllBytes(result.JournalPath, data[..^8]); }
        if (damage == "tamper") File.WriteAllText(result.JournalPath, File.ReadAllText(result.JournalPath).Replace("Completed", "Unknown"));
        if (damage == "missing-key") File.Delete(Path.Combine(Audit,"keys", plan.OperationId + ".key"));
        if (damage == "result-tamper") File.WriteAllText(result.AuditPath + ".result.json", "{}");
        Assert.True(SecureDeleteJournal.Reconcile(Audit, plan.OperationId).RequiresManualReview);
    }

    [Fact] public async Task ReplacementAtOldPathIsPreservedByReconciliation()
    {
        var path = Fixture(); var service = Service(); var plan = service.PlanDryRun([path]);
        await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다");
        File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(8192)); var hash = Hash(path);
        var result = SecureDeleteJournal.Reconcile(Audit, plan.OperationId);
        Assert.True(result.RequiresManualReview); Assert.Equal(hash, Hash(path));
        Assert.Contains(result.Items, r => r.Observation is "TargetIdentityChanged" or "PartialWriteObserved");
    }

    [Fact] public async Task IdenticalOperationCannotExecuteTwice()
    {
        var path = Fixture(); var service = Service(); var plan = service.PlanDryRun([path]);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다", cancellationToken: cts.Token);
        await Assert.ThrowsAsync<IOException>(() => service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다"));
        Assert.True(File.Exists(path));
    }

    [Theory][InlineData("../../outside")][InlineData("secure-delete-evil")]
    public void ReconciliationRejectsUntrustedOperationNames(string operation)
        => Assert.Throws<InvalidDataException>(() => SecureDeleteJournal.Reconcile(Audit,operation));

    [Fact] public void JournalRejectsInvalidTransitionAndEmptyAudit()
    {
        SecureDeleteAuditChain.PrepareDirectory(Audit);
        var operation="secure-delete-20261014-000000-"+Guid.NewGuid().ToString("N");
        var key=SecureDeleteAuditChain.EnsureOperationKey(operation,Audit);
        try { using var journal=new SecureDeleteJournal(Audit,operation,key); Assert.Throws<InvalidDataException>(()=>journal.Append(0,"DeleteRequested")); }
        finally {CryptographicOperations.ZeroMemory(key);}
        Assert.False(SecureDeleteAuditChain.Verify(Path.Combine(Audit,"absent.log"),new byte[32]));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }
}
