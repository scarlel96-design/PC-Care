using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

if(args.Length==0)return 2;
var legacy=args[0]=="--legacy-restore";
if(legacy ? args.Length!=5 : args.Length!=4||args[0]!="--transition")return 2;
if(!int.TryParse(args[^1],out var caller)||caller<=0)return 2;
if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))return 3;
try
{
    if(!string.Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\'),BootstrapDispatch.BridgeRoot,StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("BRIDGE-LOCATION");
    var leases=BootstrapDispatch.PinBridge();
    try
    {
        var install=Path.GetFullPath(args[1]);UpdateBootstrap.ValidateInstallRoot(install);
        if(legacy)
        {
            using var file=WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(args[2]))!,Path.GetFileName(args[2]));
            if(file.Stream.Length>1024*1024)throw new InvalidDataException("LEGACY-REQUEST-LIMIT");
            var request=JsonSerializer.Deserialize<LegacyRestoreRequest>(file.Stream,new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow})!;
            if(request.Plan.ConsentBinding!=args[3])throw new InvalidDataException("LEGACY-APPROVAL-BINDING");
            try{if(!Process.GetProcessById(caller).WaitForExit(TimeSpan.FromSeconds(90)))return 12;}catch(ArgumentException){}
            file.Validate();
            var result=new UpdateTransaction(BuildPublisherTrust.Current,UpdateBootstrap.WorkRoot,true)
                .ExecuteLegacyRecovery(install,request.Plan,request.OriginalPackage,request.PackagePreviousVersion,args[3],true);
            Console.WriteLine("LEGACY-RESTORE:"+result.Stage+":"+result.Code);
            return result.Stage==UpdateStage.Restored?0:10;
        }
        var version=FileVersionInfo.GetVersionInfo(Path.Combine(install,"PCCare.exe")).FileVersion;
        if(string.IsNullOrWhiteSpace(version))return 4;
        var snapshot=UpdateBootstrap.PrepareTransition(install,args[2],version,caller);
        using var process=UpdateBootstrap.Launch(snapshot,Path.GetFileName(snapshot)[10..]);
        if(process is null)return 5;
        Console.WriteLine("TRANSITION-SNAPSHOT-LAUNCHED");return 0;
    }
    finally{foreach(var lease in leases)lease.Dispose();}
}
catch(Exception ex){Console.Error.WriteLine("TRANSITION-BLOCKED:"+ex.GetType().Name+":"+ex.Message);return 6;}
