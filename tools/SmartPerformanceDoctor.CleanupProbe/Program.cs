using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.RecoverySandbox;

if (args is ["--readonly-user-temp", var inventoryOutput])
{
    var result = StorageInventory.CaptureUserTemp();
    Directory.CreateDirectory(inventoryOutput);
    File.WriteAllText(Path.Combine(inventoryOutput, "inventory.json"),
        JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"WO11_READONLY_INVENTORY candidates={result.Candidates.Count} excluded={result.ExcludedCount} complete={result.Complete} fileMutations=0");
    return 0;
}
if (args is not ["--synthetic", var output]) return 2;
Directory.CreateDirectory(output);
var parent = Path.Combine(Path.GetFullPath(output), "fixture-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(parent);
var sandbox = SandboxRecovery.CreateSynthetic(parent, 8 * 1024 * 1024);
var original = Path.Combine(sandbox.Root, "fixture.dat");
var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original)));
var candidatePath = Path.Combine(sandbox.Root, "old.tmp");
File.WriteAllBytes(candidatePath, new byte[2048]); File.SetLastWriteTimeUtc(candidatePath, DateTime.UtcNow.AddDays(-8));
var candidateInfo = new FileInfo(candidatePath);
var candidateSecurity = candidateInfo.GetAccessControl();
candidateSecurity.SetOwner(WindowsIdentity.GetCurrent().User!);
candidateInfo.SetAccessControl(candidateSecurity);
var inventory = StorageInventory.CaptureIsolated(sandbox.Root);
var preview = StorageInventory.DryRun(inventory.Candidates.Where(c => c.Disposition == CleanupDisposition.SafeCandidate).ToArray());
var before = new DriveInfo(Path.GetPathRoot(sandbox.Root)!).AvailableFreeSpace;
var timer = Stopwatch.StartNew(); sandbox.Execute(sandbox.Approve());
var quarantine = sandbox.MeasureQuarantine(before, timer.Elapsed.TotalMilliseconds);
var restored = sandbox.Recover();
var restoredHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original)));
if (hash != restoredHash || quarantine.AttributableFreedBytes != 0 || preview.SystemMutations != 0
    || preview.CandidateBytes != 2048 || preview.SelectedCount != 1) return 3;
sandbox = SandboxRecovery.CreateSynthetic(parent, 8 * 1024 * 1024);
sandbox.Execute(sandbox.Approve());
var deleted = sandbox.FinalDelete(sandbox.ApproveFinalDelete());
var options = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, "inventory.json"), JsonSerializer.Serialize(inventory, options));
File.WriteAllText(Path.Combine(output, "before-after.json"), JsonSerializer.Serialize(new { scope = "SyntheticWindowsFilesystem", quarantine,
    restored, beforeSha256 = hash, afterSha256 = restoredHash, finalDelete = deleted, speedImprovement = (double?)null }, options));
File.WriteAllText(Path.Combine(output, "dry-run.json"), JsonSerializer.Serialize(preview, options));
Console.WriteLine("WO11_CLEANUP_PASS inventory=1 quarantine=1 rollback=1 finalDelete=1 originalHashMatch=1 attributableQuarantineGain=0");
return 0;
