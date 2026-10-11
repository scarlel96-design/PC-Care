using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.RecoverySandbox;
using System.Security.Cryptography;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO11StorageCareTests : IDisposable
{
    private readonly string parent = Path.Combine(Path.GetTempPath(), "wo11-" + Guid.NewGuid().ToString("N"));
    public WO11StorageCareTests() => Directory.CreateDirectory(parent);
    private SandboxRecovery New(int length = 4096) => SandboxRecovery.CreateSynthetic(parent, length);
    [Theory]
    [InlineData("old.tmp", true, FileAttributes.Normal, 8, CleanupDisposition.SafeCandidate)]
    [InlineData("old.cache", true, FileAttributes.Normal, 8, CleanupDisposition.SafeCandidate)]
    [InlineData("old.TMP", true, FileAttributes.Normal, 8, CleanupDisposition.SafeCandidate)]
    [InlineData("new.tmp", true, FileAttributes.Normal, 1, CleanupDisposition.RequiresReview)]
    [InlineData("future.tmp", true, FileAttributes.Normal, -1, CleanupDisposition.RequiresReview)]
    [InlineData("other.tmp", false, FileAttributes.Normal, 8, CleanupDisposition.UnknownOwnership)]
    [InlineData("readonly.tmp", true, FileAttributes.ReadOnly, 8, CleanupDisposition.RequiresReview)]
    [InlineData("user.docx", true, FileAttributes.Normal, 8, CleanupDisposition.RequiresReview)]
    [InlineData("user.AV3", true, FileAttributes.Normal, 8, CleanupDisposition.Protected)]
    [InlineData("user.vault", true, FileAttributes.Normal, 8, CleanupDisposition.Protected)]
    [InlineData("app.dll", true, FileAttributes.Normal, 8, CleanupDisposition.Protected)]
    [InlineData("app.exe", true, FileAttributes.Normal, 8, CleanupDisposition.Protected)]
    [InlineData("backup.bak", true, FileAttributes.Normal, 8, CleanupDisposition.Protected)]
    [InlineData("link.tmp", true, FileAttributes.ReparsePoint, 8, CleanupDisposition.Protected)]
    [InlineData("system.tmp", true, FileAttributes.System, 8, CleanupDisposition.Protected)]
    public void ConservativeCandidatePolicy(string name, bool owned, FileAttributes attributes, int days, CleanupDisposition expected)
    { var now = DateTime.UtcNow; Assert.Equal(expected, StorageInventory.Classify(name, owned, attributes, now.AddDays(-days), now)); }

    private CleanupCandidate Candidate(SandboxRecovery sandbox)
    {
        var file = Path.Combine(sandbox.Root, "old.tmp");
        File.WriteAllBytes(file, new byte[1234]); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
        return Assert.Single(StorageInventory.CaptureIsolated(sandbox.Root).Candidates, c => c.Disposition == CleanupDisposition.SafeCandidate);
    }
    [Fact] public void InventoryUsesExactBytesWithoutEstimation()
    { var s = New(); var c = Candidate(s); Assert.Equal(1234, c.LogicalBytes); Assert.NotNull(c.Identity); Assert.False(c.CanExecute); }
    [Fact] public void InventoryNeverEntersSubdirectories()
    { var s = New(); Directory.CreateDirectory(Path.Combine(s.Root, "vault")); File.WriteAllText(Path.Combine(s.Root, "vault", "secret.tmp"), "secret"); var result = StorageInventory.CaptureIsolated(s.Root); Assert.DoesNotContain(result.Candidates, c => c.RelativePath.Contains("secret")); Assert.Contains(result.Candidates, c => c.Disposition == CleanupDisposition.Protected); }
    [Fact] public void InventoryBudgetReportsMissingRange()
    { var s = New(); var result = StorageInventory.CaptureIsolated(s.Root, 1); Assert.False(result.Complete); Assert.Single(result.Candidates); Assert.Contains("누락", result.Limitation); }
    [Fact] public void InventoryCancellationPropagates()
    { var s = New(); using var cts = new CancellationTokenSource(); cts.Cancel(); Assert.Throws<OperationCanceledException>(() => StorageInventory.CaptureIsolated(s.Root, token: cts.Token)); }
    [Fact] public void ArbitraryInventoryScopeIsRejected() => Assert.Throws<InvalidDataException>(() => StorageInventory.CaptureIsolated(parent));
    [Fact] public void DryRunHasNoSystemMutationOrExecution()
    { var s = New(); var candidate = Candidate(s); var before = File.ReadAllBytes(Path.Combine(s.Root, "old.tmp")); var result = StorageInventory.DryRun([candidate]); Assert.Equal(1234, result.CandidateBytes); Assert.False(result.ExecutionEnabled); Assert.Null(result.ActualFreedBytes); Assert.Equal(0, result.SystemMutations); Assert.Equal(before, File.ReadAllBytes(Path.Combine(s.Root, "old.tmp"))); }
    [Fact] public void ChangedTargetIsExcludedAtDryRun()
    { var s = New(); var c = Candidate(s); File.WriteAllText(Path.Combine(s.Root, "old.tmp"), "changed"); Assert.Equal(0, StorageInventory.DryRun([c]).CandidateBytes); }
    [Fact] public void ReplacedTargetIsExcludedAtDryRun()
    { var s = New(); var c = Candidate(s); File.Move(Path.Combine(s.Root, "old.tmp"), Path.Combine(s.Root, "saved.tmp")); File.WriteAllBytes(Path.Combine(s.Root, "old.tmp"), new byte[1234]); Assert.Equal(0, StorageInventory.DryRun([c]).CandidateBytes); }
    [Fact] public void DuplicateSelectionIsBlocked()
    { var c = Candidate(New()); Assert.Equal("Blocked", StorageInventory.DryRun([c, c]).Status); }
    [Fact] public void ReadOnlyChangeIsRecheckedAtPreview()
    { var s = New(); var c = Candidate(s); var path = Path.Combine(s.Root, c.RelativePath); File.SetAttributes(path, FileAttributes.ReadOnly); try { Assert.Equal(0, StorageInventory.DryRun([c]).CandidateBytes); } finally { File.SetAttributes(path, FileAttributes.Normal); } }
    [Fact] public void SyntheticRootRequiresExactGuid()
        => Assert.Throws<InvalidDataException>(() => StorageInventory.CaptureIsolated(Path.Combine(parent, "pccare-wo10-arbitrary")));
    [Fact] public void BackendRejectsForgedLegacyExecutionAuthority()
    {
        var s = New(); var path = Path.Combine(s.Root, "fixture.dat"); var before = File.ReadAllBytes(path);
        var method = typeof(SystemCareService).GetMethod("TryApplyFinding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var finding = new CareFinding { Id = "junk.temp.user", TargetPath = path, CanAutoApply = true, RiskCode = "safe" };
        Assert.Equal(false, method.Invoke(null, [finding, s.Root, s.Root, CareModuleKind.Junk]));
        Assert.Equal(before, File.ReadAllBytes(path));
    }
    [Fact] public void ProtectedSelectionHasNoEligibleBytes()
    { var c = Candidate(New()) with { Disposition = CleanupDisposition.Protected }; Assert.Equal(0, StorageInventory.DryRun([c]).CandidateBytes); }
    [Fact] public void LockedFileReportsInUse()
    { var s = New(); var c = Candidate(s); using var held = File.Open(Path.Combine(s.Root, "old.tmp"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); var result = StorageInventory.CaptureIsolated(s.Root); Assert.Contains(result.Candidates, r => r.RelativePath == c.RelativePath && r.Disposition == CleanupDisposition.InUse); Assert.False(result.Complete); }
    [Theory][InlineData(4096)][InlineData(65537)][InlineData(1048576)]
    public void SizedSyntheticRoundTripPreservesBytes(int length)
    { var s = New(length); var bytes = File.ReadAllBytes(Path.Combine(s.Root, "fixture.dat")); s.Execute(s.Approve()); Assert.Equal("Restored", s.Recover().State); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(s.Root, "fixture.dat"))); }
    [Theory][InlineData(0)][InlineData(33554433)]
    public void FixtureSizeBudgetIsEnforced(int bytes) => Assert.Throws<ArgumentOutOfRangeException>(() => New(bytes));
    [Fact] public void SameVolumeQuarantineReportsZeroAttributableFreedBytes()
    { var s = New(65536); var before = new DriveInfo(Path.GetPathRoot(s.Root)!).AvailableFreeSpace; s.Execute(s.Approve()); var result = s.MeasureQuarantine(before, 1); Assert.Equal(65536, result.QuarantinedBytes); Assert.Equal(65536, result.RestorableBytes); Assert.Equal(0, result.AttributableFreedBytes); Assert.Equal(0, result.FinalDeletedBytes); s.Recover(); }
    [Fact] public void FinalDeleteRequiresSeparateApproval()
    { var s = New(); s.Execute(s.Approve()); Assert.Throws<InvalidOperationException>(() => s.FinalDelete(s.ApproveFinalDelete() with { Approved = false })); Assert.True(File.Exists(Path.Combine(s.Root, "fixture.quarantine"))); s.Recover(); }
    [Fact] public void FinalDeleteRejectsExpiredConsentAfterPreflight()
    { var s = New(); s.Execute(s.Approve()); Assert.Throws<InvalidOperationException>(() => s.FinalDelete(s.ApproveFinalDelete() with { ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1) })); s.Recover(); }
    [Fact] public void FinalDeleteRejectsDifferentTargetIdentity()
    { var s = New(); s.Execute(s.Approve()); Assert.Throws<InvalidOperationException>(() => s.FinalDelete(s.ApproveFinalDelete() with { FileId = "forged" })); s.Recover(); }
    [Fact] public void FinalDeleteRunsOnlyOnVerifiedSyntheticQuarantine()
    { var s = New(1048576); s.Execute(s.Approve()); var result = s.FinalDelete(s.ApproveFinalDelete()); Assert.Equal(1048576, result.FinalDeletedBytes); Assert.Equal(0, result.RestorableBytes); Assert.False(File.Exists(Path.Combine(s.Root, "fixture.quarantine"))); Assert.Throws<InvalidDataException>(() => s.Recover()); }
    [Fact] public void ChangedQuarantineBlocksFinalDelete()
    { var s = New(); s.Execute(s.Approve()); var consent = s.ApproveFinalDelete(); File.WriteAllText(Path.Combine(s.Root, "fixture.quarantine"), "corrupt"); Assert.Throws<InvalidDataException>(() => s.FinalDelete(consent)); Assert.True(File.Exists(Path.Combine(s.Root, "fixture.quarantine"))); }
    [Theory][InlineData("DeletePrepared")][InlineData("Deleted")]
    public void InterruptedFinalDeletePreservesEvidenceAndRejectsRollback(string point)
    { var s = New(); s.Execute(s.Approve()); Assert.Throws<IOException>(() => s.FinalDelete(s.ApproveFinalDelete(), p => { if (p == point) throw new IOException("injected"); })); Assert.True(File.Exists(Path.Combine(s.Root, "execution.json"))); Assert.Throws<InvalidDataException>(() => s.Recover()); }
    [Fact] public void LegacyCleanupFindingCannotEnableExecution()
    { var finding = new CareFinding { Id = "junk.recycle", CanAutoApply = true, RiskCode = "safe", Confidence = .99 }; Assert.False(Assert.Single(CareFindingQualityGate.Evaluate([finding])).CanAutoApply); }
    [Fact] public void ProductHasNoFinalDeleteExecutor()
    { var assembly = typeof(StorageInventory).Assembly; Assert.DoesNotContain(assembly.GetTypes(), t => t.Name == nameof(SandboxRecovery)); }
    [Fact] public void InventoryExportOmitsUserPaths()
    { var s = New(); var c = Candidate(s); var json = System.Text.Json.JsonSerializer.Serialize(c); Assert.DoesNotContain(s.Root, json); Assert.DoesNotContain("old.tmp", json); }
    public void Dispose() { if (Directory.Exists(parent)) Directory.Delete(parent, true); }
}
