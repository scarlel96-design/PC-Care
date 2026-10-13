using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.RecoverySandbox;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO17StorageDeleteTests : IDisposable
{
    private readonly string parent = Path.Combine(Path.GetTempPath(), "wo17-" + Guid.NewGuid().ToString("N"));
    public WO17StorageDeleteTests() => Directory.CreateDirectory(parent);
    private string FileFixture(int bytes = 4096)
    {
        var path = Path.Combine(parent, Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(bytes));
        return path;
    }
    [Theory]
    [InlineData("vault/ordinary.tmp")][InlineData("anchors/ordinary.cache")]
    [InlineData("transactions/payload.tmp")][InlineData("backups/a.tmp")]
    [InlineData("receipts/x.cache")][InlineData("journal.json")]
    [InlineData("vault.header.tmp")][InlineData("recovery_slots.new")]
    [InlineData("state.dpapi.old")][InlineData("secret.pem")][InlineData("key.pfx")]
    public void ProtectedAncestorsCannotBecomeCandidates(string relative)
    {
        Assert.True(StorageTargetPolicy.IsProtected(relative));
        Assert.Equal(CleanupDisposition.Protected, StorageInventory.Classify(relative, true, FileAttributes.Normal,
            DateTime.UtcNow.AddDays(-9), DateTime.UtcNow));
    }
    [Fact] public void HiddenFileRequiresProtection() => Assert.Equal(CleanupDisposition.Protected,
        StorageInventory.Classify("old.tmp", true, FileAttributes.Hidden, DateTime.UtcNow.AddDays(-9), DateTime.UtcNow));
    [Fact] public void BoundedChildInventoryPreservesProtectedTree()
    {
        var sandbox = SandboxRecovery.Create(parent);
        Directory.CreateDirectory(Path.Combine(sandbox.Root, "cache"));
        Directory.CreateDirectory(Path.Combine(sandbox.Root, "anchors"));
        File.WriteAllBytes(Path.Combine(sandbox.Root, "cache", "old.tmp"), new byte[4097]);
        File.WriteAllBytes(Path.Combine(sandbox.Root, "anchors", "old.tmp"), new byte[4097]);
        var result = StorageInventory.CaptureIsolated(sandbox.Root, policy: new StorageScanPolicy());
        var file = Assert.Single(result.Candidates, c => c.RelativePath == Path.Combine("cache", "old.tmp"));
        Assert.Equal(4097, file.LogicalBytes);
        Assert.NotNull(file.AllocatedBytes);
        Assert.DoesNotContain(result.Candidates, c => c.RelativePath == Path.Combine("anchors", "old.tmp"));
    }
    [Fact] public void DepthBudgetReportsMissingRange()
    {
        var sandbox = SandboxRecovery.Create(parent);
        Directory.CreateDirectory(Path.Combine(sandbox.Root, "cache", "deep"));
        var result = StorageInventory.CaptureIsolated(sandbox.Root, policy: new StorageScanPolicy(1));
        Assert.False(result.Complete); Assert.Contains("DepthBudget", result.MissingReasons);
    }
    [Theory][InlineData(-1,512,5000)][InlineData(5,512,5000)][InlineData(2,513,5000)][InlineData(2,512,5001)]
    public void UnlimitedScanPolicyRejected(int depth, int entries, int ms)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new StorageScanPolicy(depth, entries, ms).Validate());
    [Fact] public void QuarantineDoesNotClaimVolumeDelta()
    {
        var result = StorageSpaceAccounting.Observe(4000, 4096, 4000, 0, 100, 900);
        Assert.Equal(0, result.VerifiedFreedBytes); Assert.Equal(800, result.UnattributedVolumeDelta);
        Assert.Equal(4000, result.RestorableBytes);
    }
    [Fact] public void FinalDeletionDoesNotAttributeExternalVolumeChanges()
        => Assert.Null(StorageSpaceAccounting.Observe(4000, 4096, 0, 4000, 100, 900).VerifiedFreedBytes);
    [Fact] public async Task SameHandleClearDeletesOnlyFixture()
    {
        var path = FileFixture(65537); var other = FileFixture(); var hash = SHA256.HashData(File.ReadAllBytes(other));
        await ForensicSecureDeleteEngine.SecureDeleteFileAsync(path, "Unknown", SecureDeleteSecurityLevel.Standard, default);
        Assert.False(File.Exists(path)); Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(other)));
    }
    [Fact] public async Task MetadataPreservingContentChangeRejected()
    {
        var path = FileFixture(); var old = ForensicSecureDeleteEngine.CaptureFingerprint(path);
        var modified = File.GetLastWriteTimeUtc(path); File.WriteAllBytes(path, new byte[4096]); File.SetLastWriteTimeUtc(path, modified);
        await Assert.ThrowsAsync<InvalidDataException>(() => ForensicSecureDeleteEngine.SecureDeleteFileAsync(path,
            "Unknown", SecureDeleteSecurityLevel.Standard, default, old));
        Assert.Equal(new byte[4096], File.ReadAllBytes(path));
    }
    [Fact] public async Task CancelBeforeOpenPreservesBytes()
    {
        var path = FileFixture(); var before = File.ReadAllBytes(path); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ForensicSecureDeleteEngine.SecureDeleteFileAsync(path,
            "Unknown", SecureDeleteSecurityLevel.Standard, cts.Token));
        Assert.Equal(before, File.ReadAllBytes(path));
    }
    [Fact] public async Task CancelAfterWritePreservesPartialFile()
    {
        var path = FileFixture(1048576); using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ForensicSecureDeleteEngine.SecureDeleteFileAsync(path,
            "Unknown", SecureDeleteSecurityLevel.Standard, cts.Token, checkpoint: p => { if(p == "Written") cts.Cancel(); }));
        Assert.True(File.Exists(path)); Assert.Equal(1048576, new FileInfo(path).Length);
    }
    [Fact] public async Task OpenedHandleRejectsReplacementAndConcurrentWrite()
    {
        var path = FileFixture(); var blocked = 0;
        await ForensicSecureDeleteEngine.SecureDeleteFileAsync(path, "Unknown", SecureDeleteSecurityLevel.Standard, default,
            checkpoint: p => { if (p != "Opened") return;
                Assert.Throws<IOException>(() => File.Move(path, path + ".replacement"));
                Assert.Throws<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose()); blocked++; });
        Assert.Equal(1, blocked); Assert.False(File.Exists(path));
    }
    [Fact] public async Task ReadOnlyFixtureIsNotSilentlyModified()
    {
        var path = FileFixture(); var before = File.ReadAllBytes(path); File.SetAttributes(path, FileAttributes.ReadOnly);
        try { await Assert.ThrowsAnyAsync<Exception>(() => ForensicSecureDeleteEngine.SecureDeleteFileAsync(path,
            "Unknown", SecureDeleteSecurityLevel.Standard, default)); Assert.Equal(before, File.ReadAllBytes(path)); }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }
    [Fact] public async Task AdsFixtureRequiresReviewAndPreservesBothStreams()
    {
        var path = FileFixture(); File.WriteAllText(path + ":wo17", "synthetic"); var before = File.ReadAllBytes(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => ForensicSecureDeleteEngine.SecureDeleteFileAsync(path,
            "Unknown", SecureDeleteSecurityLevel.Standard, default));
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.Equal("synthetic", File.ReadAllText(path + ":wo17"));
    }
    [Fact] public async Task DetailedCancellationDoesNotReportCompleted()
    {
        var path = FileFixture(); var service = new ProfessionalSecureDeleteService(parent); var plan = service.PlanDryRun([path]);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다", cancellationToken: cts.Token);
        Assert.Equal("Cancelled", result.Status); Assert.Equal(0, result.Deleted); Assert.True(File.Exists(path));
    }
    [Fact] public void ProtectedVaultPathRejectedAtDeleteSelection()
    { Assert.False(PathSafetyGuard.Evaluate(Path.Combine(parent,"vault","ordinary.tmp")).Allowed); }
    [Fact] public void UnknownAllocationRemainsUnknown() => Assert.Null(StorageSpaceAccounting.Observe(1,null,0,0,null,null).AllocatedBytes);
    [Fact] public void MissingDeleteTargetIsVisible()
    { var plan = new ProfessionalSecureDeleteService(parent).PlanDryRun([Path.Combine(parent,"missing.tmp")]); Assert.Empty(plan.Targets); Assert.Single(plan.BlockedTargets); }
    [Fact] public async Task ArbitraryOperationPathRejectedBeforeAuditWrite()
    {
        var path = FileFixture(); var service = new ProfessionalSecureDeleteService(parent); var plan = service.PlanDryRun([path]);
        var forged = new SmartPerformanceDoctor.App.Models.Commercial.SecureDeletePlan { OperationId = "../forged", Targets = plan.Targets };
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ApplyDetailedAsync(forged,"보안 삭제에 동의합니다")); Assert.True(File.Exists(path));
    }
    [Fact] public async Task MissingIdentityCannotAuthorizeDelete()
    {
        var path = FileFixture(); var service = new ProfessionalSecureDeleteService(parent); var plan = service.PlanDryRun([path]);
        var forged = new SmartPerformanceDoctor.App.Models.Commercial.SecureDeletePlan { OperationId = plan.OperationId,
            Targets = [new SmartPerformanceDoctor.App.Models.Commercial.SecureDeleteTarget { Path = path }] };
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ApplyDetailedAsync(forged,"보안 삭제에 동의합니다")); Assert.True(File.Exists(path));
    }
    [Fact] public async Task OperationCannotRunTwice()
    {
        var path = FileFixture(); var service = new ProfessionalSecureDeleteService(parent); var plan = service.PlanDryRun([path]);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await service.ApplyDetailedAsync(plan,"보안 삭제에 동의합니다",cancellationToken:cts.Token);
        await Assert.ThrowsAsync<IOException>(() => service.ApplyDetailedAsync(plan,"보안 삭제에 동의합니다")); Assert.True(File.Exists(path));
    }
    [Fact] public async Task FolderExecutionPreservesAllFilesUntilQualified()
    {
        var folder = Path.Combine(parent,"unqualified-folder"); Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,"data.tmp"),new byte[4096]);
        var service = new ProfessionalSecureDeleteService(parent); var plan = service.PlanDryRun([folder]);
        var result = await service.ApplyDetailedAsync(plan,"보안 삭제에 동의합니다");
        Assert.Equal("Failed",result.Status); Assert.Equal(0,result.Deleted); Assert.True(File.Exists(Path.Combine(folder,"data.tmp")));
    }
    [Fact] public void DuplicateFileIdentitySelectionIsBlocked()
    {
        var s = SandboxRecovery.Create(parent); var path = Path.Combine(s.Root,"old.tmp"); File.WriteAllBytes(path,new byte[4096]);
        File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-9)); var candidate = StorageInventory.CaptureIsolated(s.Root).Candidates.Single(c=>c.RelativePath=="old.tmp");
        Assert.Equal("Blocked",StorageInventory.DryRun([candidate,candidate with {CandidateId=Guid.NewGuid().ToString("N")}]).Status);
    }
    [Theory][InlineData(FileAttributes.SparseFile)][InlineData(FileAttributes.Compressed)][InlineData(FileAttributes.Offline)]
    public void SpecialAllocationNeverAutoQualifies(FileAttributes attributes)
        => Assert.Equal(CleanupDisposition.RequiresReview,StorageInventory.Classify("old.tmp",true,attributes,DateTime.UtcNow.AddDays(-9),DateTime.UtcNow));
    public void Dispose() { if (Directory.Exists(parent)) Directory.Delete(parent, true); }
}
