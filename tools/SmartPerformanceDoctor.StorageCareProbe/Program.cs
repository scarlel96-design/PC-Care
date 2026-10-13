using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.RecoverySandbox;

if (args is not ["--synthetic", var output, var fixtureParent]) return 2;
var evidence = Path.GetFullPath(output);
Directory.CreateDirectory(evidence);
var parent = Path.Combine(Path.GetFullPath(fixtureParent), "wo17-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(parent);
void Save(string name, object value) => File.WriteAllText(Path.Combine(evidence, name), JsonSerializer.Serialize(value,
    new JsonSerializerOptions { WriteIndented = true }).Replace(parent, "<SYNTHETIC_ROOT>", StringComparison.OrdinalIgnoreCase));
try
{
    var sandbox = SandboxRecovery.CreateSynthetic(parent, 1048576);
    var target = Path.Combine(sandbox.Root, "old.tmp");
    File.WriteAllBytes(target, RandomNumberGenerator.GetBytes(65537));
    File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddDays(-9));
    var ownerInfo = new FileInfo(target);
    var acl = System.IO.FileSystemAclExtensions.GetAccessControl(ownerInfo);
    acl.SetOwner(System.Security.Principal.WindowsIdentity.GetCurrent().User!);
    System.IO.FileSystemAclExtensions.SetAccessControl(ownerInfo, acl);
    var sw = Stopwatch.StartNew();
    var inventory = StorageInventory.CaptureIsolated(sandbox.Root, policy: new StorageScanPolicy());
    Save("STORAGE_INVENTORY_RESULT.json", inventory);
    var candidate = inventory.Candidates.Single(c => c.RelativePath == "old.tmp");
    var preview = StorageInventory.DryRun([candidate]);
    Save("DRY_RUN_RESULT.json", preview);
    var beforeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(sandbox.Root, "fixture.dat"))));
    var freeBefore = new DriveInfo(Path.GetPathRoot(sandbox.Root)!).AvailableFreeSpace;
    sandbox.Execute(sandbox.Approve());
    var quarantined = sandbox.MeasureQuarantine(freeBefore, sw.Elapsed.TotalMilliseconds);
    var recovery = sandbox.Recover();
    var afterHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(sandbox.Root, "fixture.dat"))));
    if (!recovery.BytesVerified || beforeHash != afterHash) throw new IOException("RoundTripMismatch");
    Save("QUARANTINE_RECOVERY_RESULT.json", new { recovery, beforeHash, afterHash, quarantined });
    Save("STORAGE_ACCOUNTING_RESULT.json", StorageSpaceAccounting.Observe(1048576, null, 1048576, 0,
        quarantined.AvailableBefore, quarantined.AvailableAfter));
    Save("PERFORMANCE_BEFORE_AFTER_RESULT.json", new { inventory.ElapsedMs, inventory.CollectorCpuMs,
        inventory.WorkingSetBytes, quarantined, SpeedImprovement = (double?)null, Conditions = "Synthetic 1MiB file; one run; no Windows speed claim" });
    var service = new ProfessionalSecureDeleteService(Path.Combine(parent, "audit-output"));
    var plan = service.PlanDryRun([target]);
    Save("SECURE_DELETE_PREFLIGHT_RESULT.json", new { TargetCount = plan.Targets.Count, BlockedCount = plan.BlockedTargets.Count,
        Checks = plan.Targets.SelectMany(t => t.Preflight!.Checks).Select(c => new {c.Code,c.Status}) });
    var result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다");
    Save("SECURE_DELETE_IDENTITY_RESULT.json", new { result.Status, result.Deleted, result.Failed, result.AuditValid,
        DeletedFixtureMissing = !File.Exists(target), OtherFixtureHash = afterHash });
    if (result.Deleted != 1 || !result.AuditValid || File.Exists(target)) throw new IOException("DeleteFailed");
    target = Path.Combine(sandbox.Root, "cancel.tmp"); File.WriteAllBytes(target, new byte[4096]);
    plan = service.PlanDryRun([target]); using var cts = new CancellationTokenSource(); cts.Cancel();
    result = await service.ApplyDetailedAsync(plan, "보안 삭제에 동의합니다", cancellationToken: cts.Token);
    Save("SECURE_DELETE_CANCELLATION_RESULT.json", new { result.Status, result.Deleted, result.Failed,
        OriginalPreserved = File.Exists(target), result.AuditValid });
    if (result.Status != "Cancelled" || !File.Exists(target)) throw new IOException("CancellationMismatch");
    Save("PROTECTED_TARGET_RESULT.json", new { VaultProtected = StorageTargetPolicy.IsProtected("vault/ordinary.tmp"),
        AnchorProtected = StorageTargetPolicy.IsProtected("anchors/ordinary.tmp"), ProductionCleanupEnabled = candidate.CanExecute });
    Save("STORAGE_MEDIA_RESULT.json", new { ActualReportedMedia = SecureDeleteStorageProfiler.Profile(target),
        FileSystem = new DriveInfo(Path.GetPathRoot(target)!).DriveFormat, PhysicalSanitizeTest = "NOT TESTED",
        Policy = new[] {"HDD: logical file Clear", "SSD/NVMe: physical erasure unverified", "Unknown: conservative Clear"} });
    Console.WriteLine("WO17_SYNTHETIC=PASS;QUARANTINE_SHA256_MATCH=TRUE;DELETE=1;CANCEL_PRESERVED=TRUE;PRODUCTION_CLEANUP=FALSE");
    return 0;
}
catch (Exception error) { Console.Error.WriteLine("WO17_PROBE_FAIL " + error.GetType().Name); return 1; }
// Preserve fixtures for local review. Do not publish fixture contents or audit keys.
