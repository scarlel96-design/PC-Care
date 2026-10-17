using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartPerformanceDoctor.UpdateEngine;
if(args.Length==4&&args[0]=="compare")
{
    QualificationObservation Read(string p){using var f=File.OpenRead(p);if(f.Length>65536)throw new InvalidDataException("EVIDENCE-LIMIT");return JsonSerializer.Deserialize<QualificationObservation>(f,new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})!;}
    var r=InstallationQualificationComparison.Compare(Read(args[1]),Read(args[2]),bool.Parse(args[3]));Console.WriteLine(JsonSerializer.Serialize(r));return r.Verified?0:10;
}
if(args.Length!=2||args[0] is not ("capture" or "access" or "strict-apply"))return 2;
var root=Path.GetFullPath(args[1]);var parent=Environment.GetEnvironmentVariable("RUNNER_TEMP");
if(string.IsNullOrWhiteSpace(parent)||!root.StartsWith(Path.GetFullPath(parent).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("wo20-"))return 3;
using var pin=WindowsFileBoundary.PinDirectory(root);
if(File.ReadAllText(Path.Combine(root,"fixture.marker"))!="WO20_SYNTHETIC_ONLY")return 3;
if(args[0]=="strict-apply")
{
    if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT")!="github-hosted")return 3;
    var installPath=Path.Combine(root,"install");var admins=new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid,null);
    var security=new System.Security.AccessControl.DirectorySecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(admins);
    foreach(var sid in new[]{admins,new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid,null)})security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(sid,System.Security.AccessControl.FileSystemRights.FullControl,System.Security.AccessControl.InheritanceFlags.ContainerInherit|System.Security.AccessControl.InheritanceFlags.ObjectInherit,System.Security.AccessControl.PropagationFlags.None,System.Security.AccessControl.AccessControlType.Allow));
    security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinUsersSid,null),System.Security.AccessControl.FileSystemRights.ReadAndExecute,System.Security.AccessControl.InheritanceFlags.ContainerInherit|System.Security.AccessControl.InheritanceFlags.ObjectInherit,System.Security.AccessControl.PropagationFlags.None,System.Security.AccessControl.AccessControlType.Allow));
    System.IO.FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(installPath),security);
    var strictPolicy=new PublisherTrustPolicy([new TrustedPublisherKey("WO20-Fixture","ephemeral","stable",File.ReadAllBytes(Path.Combine(root,"public-key.bin")))]);
    // Use the production staging contract only in the disposable hosted runner.
    var productRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PC-Care");
    if(Directory.Exists(productRoot))throw new InvalidOperationException("CI_STAGING_ALREADY_EXISTS");
    var protectedWork=Path.Combine(productRoot,"UpdateTransactions");
    var strictEngine=new UpdateTransaction(strictPolicy,protectedWork,true){PerformanceProfile=new()};var process=System.Diagnostics.Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var allocated=GC.GetTotalAllocatedBytes(true);var timer=System.Diagnostics.Stopwatch.StartNew();
    var result=strictEngine.Apply(Path.Combine(root,"update.spdup"),installPath,"51.0.4");timer.Stop();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;var bytes=GC.GetTotalAllocatedBytes(true)-allocated;var strictHistory=strictEngine.InspectInstallationHistory(installPath);
    int? restoredOriginals=null;int? newFilesAbsent=null;
    if(result.Stage==UpdateStage.Restored)
    {
        using var oldPackage=File.OpenRead(Path.Combine(root,"original.spdup"));
        using var newPackage=File.OpenRead(Path.Combine(root,"update.spdup"));
        var oldDeclaration=SignedPackage.Verify(oldPackage,"51.0.3",strictPolicy);
        var newDeclaration=SignedPackage.Verify(newPackage,"51.0.4",strictPolicy);
        if(!oldDeclaration.CanApply||oldDeclaration.Manifest is null||!newDeclaration.CanApply||newDeclaration.Manifest is null)
            throw new InvalidDataException("ROLLBACK_REFERENCE_REJECTED");
        var previousPaths=oldDeclaration.Manifest.Files.Select(x=>x.Path).ToHashSet(StringComparer.Ordinal);
        restoredOriginals=0;newFilesAbsent=0;
        foreach(var expected in oldDeclaration.Manifest.Files)
        {
            using var file=WindowsFileBoundary.OpenRead(installPath,expected.Path[8..]);file.Verify(expected.Length,expected.Sha256);restoredOriginals++;
        }
        foreach(var expected in newDeclaration.Manifest.Files.Where(x=>!previousPaths.Contains(x.Path)))
        {
            var target=Path.Combine(installPath,expected.Path[8..]);
            if(File.Exists(target)||Directory.Exists(target))throw new InvalidDataException("ROLLBACK_NEW_FILE_REMAINS");newFilesAbsent++;
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(new{result.Code,Stage=result.Stage.ToString(),result.Failure,RestoredOriginals=restoredOriginals,NewFilesAbsent=newFilesAbsent,
        InstallVolume=Path.GetPathRoot(installPath),StagingVolume=Path.GetPathRoot(protectedWork),ElapsedMs=timer.Elapsed.TotalMilliseconds,CpuMs=cpuMs,AllocatedBytes=bytes,PeakWorkingSetBytes=process.PeakWorkingSet64,StrictAcl=true,History=strictHistory,Phases=strictEngine.PerformanceProfile.Snapshot(),Scope="CI protected synthetic files and fresh fixed ProgramData staging; no PE deployment or UAC"}));return result.Stage==UpdateStage.Committed&&strictHistory.Verified?0:10;
}
if(args[0]=="access")
{
    var identity=System.Security.Principal.WindowsIdentity.GetCurrent();
    var admin=new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    var observations=new List<object>();
    foreach(var name in new[]{"install","work"})
    {
        using var boundary=WindowsFileBoundary.PinDirectory(Path.Combine(root,name));var relative="wo24-access-"+Guid.NewGuid().ToString("N");var permitted=false;
        try{using(var file=WindowsFileBoundary.OpenWrite(Path.Combine(root,name),relative,true)){permitted=true;}File.Delete(Path.Combine(root,name,relative));}
        catch(UnauthorizedAccessException){}boundary.Validate();observations.Add(new{Target=name,CreatePermitted=permitted});
    }
    Console.WriteLine(JsonSerializer.Serialize(new{Scope="Actual synthetic-root create attempts under current token; independent SID pair not established",Administrator=admin,SidHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.User!.Value))),Targets=observations}));return 0;
}
var machine=Environment.GetEnvironmentVariable("WO24_MACHINE");var boot=Environment.GetEnvironmentVariable("WO24_BOOT");var source=Environment.GetEnvironmentVariable("WO24_SOURCE");
if(new[]{machine,boot,source}.Any(string.IsNullOrWhiteSpace))return 3;
var install=Path.Combine(root,"install");using var installation=WindowsFileBoundary.PinDirectory(install);
var policy=new PublisherTrustPolicy([new TrustedPublisherKey("WO20-Fixture","ephemeral","stable",File.ReadAllBytes(Path.Combine(root,"public-key.bin")))]);
var engine=new UpdateTransaction(policy,Path.Combine(root,"work"),false);var history=engine.InspectInstallationHistory(install);var recovery=engine.InspectRecovery(install);
var held=new List<WindowsFileBoundary.FileLease>();
try{
var files=new SortedDictionary<string,string>(StringComparer.Ordinal);var pending=new Stack<(string Path,int Depth)>();pending.Push((install,0));
while(pending.TryPop(out var entry))
{
    if(entry.Depth>32)throw new InvalidDataException("DEPTH-LIMIT");using var directory=WindowsFileBoundary.PinDirectory(entry.Path);
    foreach(var path in Directory.EnumerateFiles(entry.Path)){if(files.Count>=4096)throw new InvalidDataException("FILE-LIMIT");var relative=Path.GetRelativePath(install,path);var file=WindowsFileBoundary.OpenRead(install,relative);held.Add(file);files[relative]=file.Hash();}
    foreach(var child in Directory.EnumerateDirectories(entry.Path))pending.Push((child,entry.Depth+1));directory.Validate();
}
history=engine.InspectInstallationHistory(install);foreach(var lease in held)lease.Validate();
installation.Validate();pin.Validate();var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files))));
Console.WriteLine(JsonSerializer.Serialize(new{Observation=new QualificationObservation(Path.GetFileName(root),machine!,boot!,source!,installation.Identity,digest,history.Verified,recovery.State.ToString()),History=history,Files=files,Scope="Read-only synthetic observation; not an authorization token or installed product E2E"}));return 0;

}finally{foreach(var lease in held)lease.Dispose();}
