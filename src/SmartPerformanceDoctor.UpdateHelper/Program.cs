using System.Diagnostics;
using System.Security.Principal;
using SmartPerformanceDoctor.UpdateEngine;

// This elevated binary has only two fixed verbs and derives its target from its own installed location.
if (args.Length < 1 || args[0] is not ("--apply" or "--recover")) return 2;
if (!OperatingSystem.IsWindows() || !new WindowsPrincipal(WindowsIdentity.GetCurrent())
    .IsInRole(WindowsBuiltInRole.Administrator)) return 3;
var engineDirectory = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
if (!string.Equals(Path.GetFileName(engineDirectory), "engine", StringComparison.OrdinalIgnoreCase)) return 4;
var installRoot = Directory.GetParent(engineDirectory)?.FullName;
if (installRoot is null || !Directory.Exists(installRoot) ||
    (File.GetAttributes(installRoot) & FileAttributes.ReparsePoint) != 0) return 5;
var programFiles = new[]
{
    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
};
if (!programFiles.Any(root => !string.IsNullOrWhiteSpace(root) && installRoot.StartsWith(
        root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase))) return 6;
var mainExe = Path.Combine(installRoot, "PCCare.exe");
var workRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "PC-Care", "UpdateTransactions");
try
{
    var coordinator = new UpdateTransaction(PublisherTrustPolicy.Production, workRoot, strictAcl: true);
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
        result = coordinator.Recover(installRoot);
    }
    else if (args[0] == "--apply" && args.Length == 4 && args[2] == "--wait-pid" &&
             int.TryParse(args[3], out var pid) && pid > 0)
    {
        try
        {
            if (!Process.GetProcessById(pid).WaitForExit(TimeSpan.FromSeconds(90))) return 12;
        }
        catch (ArgumentException) { /* caller already exited */ }
        var recovery = coordinator.Recover(installRoot);
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
    return args[0] == "--recover"
        ? result.Stage == UpdateStage.Restored ? 0 : 10
        : result.Stage == UpdateStage.Committed ? 0 : 10;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
    return 11;
}
