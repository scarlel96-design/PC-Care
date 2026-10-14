using System.Diagnostics;
using System.Security.Principal;
using SmartPerformanceDoctor.UpdateEngine;

// This tool runs outside the installation. It never trusts a command-supplied helper executable.
if (args.Length != 4 || args[0] != "--transition" || !int.TryParse(args[3], out var caller) || caller <= 0) return 2;
if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return 3;
try
{
    foreach (var name in new[] { "SmartPerformanceDoctor.UpdateBridge.exe", "SmartPerformanceDoctor.UpdateBridge.dll", "SmartPerformanceDoctor.UpdateEngine.dll" })
    {
        if (!AuthenticodeBoundary.IsBuildPublisher(AuthenticodeBoundary.Verify(Path.Combine(AppContext.BaseDirectory, name))))
            throw new InvalidDataException("BRIDGE-PUBLISHER-UNVERIFIED");
    }
    var install = Path.GetFullPath(args[1]);
    UpdateBootstrap.ValidateInstallRoot(install);
    var version = FileVersionInfo.GetVersionInfo(Path.Combine(install, "PCCare.exe")).FileVersion;
    if (string.IsNullOrWhiteSpace(version)) return 4;
    var snapshot = UpdateBootstrap.PrepareTransition(install, args[2], version, caller);
    using var process = UpdateBootstrap.Launch(snapshot, Path.GetFileName(snapshot)[10..]);
    if (process is null) return 5;
    Console.WriteLine("TRANSITION-SNAPSHOT-LAUNCHED");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("TRANSITION-BLOCKED:" + ex.GetType().Name + ":" + ex.Message);
    return 6;
}
