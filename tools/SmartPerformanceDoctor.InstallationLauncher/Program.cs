using System.Diagnostics;
using System.Security.AccessControl;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

// This managed launcher does not establish a native pre-CLR trust boundary.
if (args.Length != 1 || args[0] is not ("--inspect" or "--launch")) return 2;
try
{
    var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PC-Care Launcher");
    var install=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PC-Care");
    if (!string.Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\'),root,StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("LAUNCHER-LOCATION");
    UpdateTransaction.ValidateProtectedSecurity(new DirectoryInfo(root).GetAccessControl(), false);
    var leases=new List<WindowsFileBoundary.FileLease>();
    try
    {
        foreach(var name in new[]{"SmartPerformanceDoctor.InstallationLauncher.exe","SmartPerformanceDoctor.InstallationLauncher.dll","SmartPerformanceDoctor.UpdateEngine.dll"})
        {
            var signature=AuthenticodeBoundary.Verify(Path.Combine(root,name));
            if(!AuthenticodeBoundary.IsBuildPublisher(signature))throw new InvalidDataException("LAUNCHER-PUBLISHER");
            var file=WindowsFileBoundary.OpenRead(root,name);leases.Add(file);
            UpdateTransaction.ValidateProtectedSecurity(file.ReadSecurity(), false);
            file.Verify(file.Stream.Length,signature.Hash,signature.Identity);
        }
        UpdateBootstrap.ValidateInstallRoot(install);
        var transaction=new UpdateTransaction(BuildPublisherTrust.Current,UpdateBootstrap.WorkRoot,true);
        var state=transaction.InspectStartupTrust(install);
        Console.WriteLine(JsonSerializer.Serialize(state));
        if(!state.Verified||state.RequiresReview)return 10;
        if(args[0]=="--inspect")return 0;
        using var executable=WindowsFileBoundary.OpenRead(install,"PCCare.exe");
        var signatureMain=AuthenticodeBoundary.Verify(Path.Combine(install,"PCCare.exe"));
        if(!AuthenticodeBoundary.IsBuildPublisher(signatureMain))return 11;
        executable.Verify(executable.Stream.Length,signatureMain.Hash,signatureMain.Identity);
        // Verify again after binding the executable. Protected installation ACLs remain mandatory.
        if(!transaction.InspectStartupTrust(install).Verified)return 12;
        using var child=Process.Start(new ProcessStartInfo(Path.Combine(install,"PCCare.exe")){UseShellExecute=false,WorkingDirectory=install});
        if(child is null)return 13;
        child.WaitForExit();return child.ExitCode;
    }
    finally{foreach(var file in leases)file.Dispose();}
}
catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
{Console.Error.WriteLine("PREEXECUTION-BLOCKED:"+ex.GetType().Name+":"+ex.Message);return 6;}
