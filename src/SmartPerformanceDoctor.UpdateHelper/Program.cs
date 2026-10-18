using System.Diagnostics;
using System.Security.Principal;
using SmartPerformanceDoctor.UpdateEngine;

// Apply files only from the authenticated protected snapshot.
if (args.Length < 1 || args[0] is not ("--apply" or "--recover" or "--bootstrap" or "--bootstrap-recover")) return 2;
if (!OperatingSystem.IsWindows() || !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return 3;
var executionDirectory = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
string installRoot;
string? bootstrapId = null;
if (args[0] is "--bootstrap" or "--bootstrap-recover")
{
    var recoveryLaunch = args[0] == "--bootstrap-recover";
    if (recoveryLaunch ? args.Length != 4 || args[2] != "--wait-pid" : args.Length != 2) return 9;
    try
    {
        var binding = UpdateBootstrap.Validate(executionDirectory, args[1]);
        var recoveryPid = 0;
        if (recoveryLaunch && (!int.TryParse(args[3], out recoveryPid) || recoveryPid <= 0)) return 9;
        foreach (var waitPid in recoveryLaunch ? new[] { recoveryPid } : new[] { binding.ParentPid, binding.CallerPid })
        {
            try { if (!Process.GetProcessById(waitPid).WaitForExit(TimeSpan.FromSeconds(90))) return 12; }
            catch (ArgumentException) { }
        }
        binding = UpdateBootstrap.Validate(executionDirectory, args[1]);
        bootstrapId = binding.Id;
        installRoot = binding.InstallRoot;
        args = !recoveryLaunch && binding.Verb == "--apply" ? ["--apply", binding.Package, "--wait-pid", binding.CallerPid.ToString()] : ["--recover"];
    }
    catch (Exception ex) { Console.Error.WriteLine("BOOTSTRAP-REJECTED: " + ex.Message); return 14; }
}
else
{
    if (!string.Equals(Path.GetFileName(executionDirectory), "engine", StringComparison.OrdinalIgnoreCase)) return 4;
    installRoot = Directory.GetParent(executionDirectory)!.FullName;
    var validApply = args.Length == 4 && args[0] == "--apply" && args[2] == "--wait-pid";
    var validRecover = args[0] == "--recover" && args.Length is 1 or 3;
    if (!validApply && !validRecover) return 9;
    if (args.Length == 3 && args[1] != "--wait-pid") return 9;
    var callerPid = int.MaxValue;
    if (args.Length > 1 && (!int.TryParse(args[^1], out callerPid) || callerPid <= 0)) return 9;
    try
    {
        var directory = UpdateBootstrap.Prepare(installRoot, args[0], validApply ? args[1] : "", callerPid);
        using var child = UpdateBootstrap.Launch(directory, Path.GetFileName(directory)[10..]);
        if (child is null) return 15;
        Console.WriteLine("BOOTSTRAP-LAUNCHED " + child.Id);
        return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine("BOOTSTRAP-REJECTED: " + ex.Message); return 14; }
}
var engineDirectory = Path.Combine(installRoot, "engine");
var mainExe = Path.Combine(installRoot, "PCCare.exe");
var workRoot = UpdateBootstrap.WorkRoot;
try
{
    var coordinator = new UpdateTransaction(BuildPublisherTrust.Current, workRoot, strictAcl: true);
    TransactionResult result;
    if (args[0] == "--recover" && args.Length is 1 or 3)
    {
        if (args.Length == 3)
        {
            if (args[1] != "--wait-pid" || !int.TryParse(args[2], out var recoveryPid) || recoveryPid <= 0)
                return 9;
            try
            {
                if (!Process.GetProcessById(recoveryPid).WaitForExit(TimeSpan.FromSeconds(90))) return 12;
            }
            catch (ArgumentException) { /* caller already exited */ }
        }
        result = coordinator.ResumeVerifiedRecovery(installRoot);
    }
    else if (args[0] == "--apply" && args.Length == 4 && args[2] == "--wait-pid" &&
             int.TryParse(args[3], out var pid) && pid > 0)
    {
        try
        {
            if (!Process.GetProcessById(pid).WaitForExit(TimeSpan.FromSeconds(90))) return 12;
        }
        catch (ArgumentException) { /* caller already exited */ }
        var recovery = coordinator.ResumeVerifiedRecovery(installRoot);
        if (recovery.Stage == UpdateStage.RecoveryRequired) return 10;
        if (recovery.Code == "ROLLED-BACK")
        {
            Console.WriteLine($"{recovery.Stage} {recovery.Code} {recovery.TransactionId}");
            return 13; // review the restored installation before another attempt
        }
        if (!File.Exists(mainExe)) return 7;
        var currentVersion = FileVersionInfo.GetVersionInfo(mainExe).FileVersion;
        if (string.IsNullOrWhiteSpace(currentVersion)) return 8;
        result = coordinator.Apply(args[1], installRoot, currentVersion,
            postApplyVersionCheck: expected =>
            {
                var installed = FileVersionInfo.GetVersionInfo(mainExe).FileVersion;
                return Version.TryParse(installed, out var actual) &&
                       Version.TryParse(expected, out var target) &&
                       actual.Major == target.Major && actual.Minor == target.Minor &&
                       actual.Build == target.Build && (target.Revision < 0 || actual.Revision == target.Revision) &&
                       File.Exists(Path.Combine(installRoot, "SmartPerformanceDoctor.dll")) &&
                       File.Exists(Path.Combine(engineDirectory, "SmartPerformanceDoctor.UpdateHelper.exe"));
            });
    }
    else return 9;
    Console.WriteLine($"{result.Stage} {result.Code} {result.TransactionId}");
    var exitCode = args[0] == "--recover"
        ? result.Stage == UpdateStage.Restored ? 0 : 10
        : result.Stage == UpdateStage.Committed ? 0 : 10;
    UpdateBootstrap.RecordOutcome(executionDirectory, bootstrapId!, result, exitCode);
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
    return 11;
}
