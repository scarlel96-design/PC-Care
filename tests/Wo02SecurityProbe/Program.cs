using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;

var root = Path.Combine(Path.GetTempPath(), "pccare-wo02-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var original = Path.Combine(root, "original.txt");
    var quarantine = Path.Combine(root, "quarantine");
    Directory.CreateDirectory(quarantine);
    var held = Path.Combine(quarantine, "held.txt");
    File.WriteAllText(original, "original");
    CareRollbackService.RecordQuarantine(root, original, held);
    File.Move(original, held); // Simulated stop before state transition.
    var recovered = CareRollbackService.Rollback(root);
    Require(recovered.Success && File.ReadAllText(original) == "original", "crash-recovery");
    Console.WriteLine("care-crash-recovery=PASS");

    File.Delete(original);
    File.WriteAllText(original, "second");
    var conflictHeld = Path.Combine(quarantine, "conflict.txt");
    CareRollbackService.RecordQuarantine(root, original, conflictHeld);
    File.Move(original, conflictHeld);
    File.WriteAllText(original, "new-user-data");
    var conflict = CareRollbackService.Rollback(root);
    Require(!conflict.Success && File.ReadAllText(original) == "new-user-data"
        && File.ReadAllText(conflictHeld) == "second", "conflict-preservation");
    Console.WriteLine("care-conflict-preservation=PASS");

    File.Delete(original);
    File.WriteAllText(conflictHeld, "tampered");
    var tamper = CareRollbackService.Rollback(root);
    Require(!tamper.Success && !File.Exists(original) && File.Exists(conflictHeld), "tamper-rejection");
    Console.WriteLine("care-tamper-rejection=PASS");

    Require(Av3PhaseGate.CurrentStatus == Av3ProductionStatus.ReadOnlyValidated
        && !Av3PhaseGate.ProductionWriterEnabled && !Av3PhaseGate.JournalWriterEnabled
        && !Av3PhaseGate.MigrationEnabled, "av3-flag-gate");
    try
    {
        Av3WriterAccessGate.DenyProductionCreate();
        throw new InvalidOperationException("AV3 production route unexpectedly open");
    }
    catch (Av3WriterRouteBlockedException)
    {
        Console.WriteLine("av3-production-writer=BLOCKED_EXPECTED");
    }
    Console.WriteLine("result=PASS");
    return 0;
}
finally
{
    Directory.Delete(root, recursive: true);
}

static void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name + " failed");
}
