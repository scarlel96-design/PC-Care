using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

if(args.Length<2)return 2;
if(args[0] is "verify" or "verify-pinned")
{
    var blocked=0;
    var result=AuthenticodeBoundary.Verify(args[1],args[0]=="verify-pinned"?()=>
    {
        try{File.Delete(args[1]);}catch(IOException){blocked++;}
        try{File.WriteAllText(args[1],"replacement");}catch(IOException){blocked++;}
    }:null);
    Console.WriteLine(JsonSerializer.Serialize(new{result.Code,result.Thumbprint,result.Identity,result.Hash,BlockedMutations=blocked,ProductionAccepted=AuthenticodeBoundary.IsBuildPublisher(result)}));return 0;
}
if(args[0]!="acl")return 2;
var root=Path.GetFullPath(args[1]);var parent=Environment.GetEnvironmentVariable("RUNNER_TEMP");
if(parent is null||!root.StartsWith(Path.GetFullPath(parent).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("wo23-")||Directory.Exists(root))return 3;
Directory.CreateDirectory(root);
var records=new List<object>();
void Pass(string name,Action action){action();records.Add(new{Name=name,Status="PASS",Input="Disposable runner synthetic filesystem",ExitCode=0});}
void Reject(Action action){try{action();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){return;}throw new InvalidDataException("Expected boundary rejection");}
try
{
    var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);var users=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
    var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(admins);
    foreach(var sid in new[]{admins,system})security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
    security.AddAccessRule(new FileSystemAccessRule(users,FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
    new DirectoryInfo(root).SetAccessControl(security);
    Pass("ProtectedOwnerAndExplicitAces",()=>UpdateTransaction.ValidateProtectedDirectory(root));
    var file=Path.Combine(root,"sample.bin");File.WriteAllText(file,"synthetic");
    Pass("InheritedFileAces",()=>UpdateTransaction.ValidateProtectedSecurity(new FileInfo(file).GetAccessControl(),false));
    var weak=new DirectoryInfo(root).GetAccessControl();weak.AddAccessRule(new FileSystemAccessRule(users,FileSystemRights.WriteData,AccessControlType.Allow));new DirectoryInfo(root).SetAccessControl(weak);
    Pass("WritableNonAdminAceRejected",()=>Reject(()=>UpdateTransaction.ValidateProtectedDirectory(root)));new DirectoryInfo(root).SetAccessControl(security);
    Pass("PinnedPathReplacementAndMutationDenied",()=>{using var lease=WindowsFileBoundary.OpenRead(root,"sample.bin");Reject(()=>File.Delete(file));Reject(()=>File.WriteAllText(file,"changed"));lease.Validate();});
    var identity="";using(var lease=WindowsFileBoundary.OpenRead(root,"sample.bin")){identity=lease.Identity;}
    var replacement=Path.Combine(root,"replacement.bin");File.WriteAllText(replacement,"synthetic");File.Move(file,Path.Combine(root,"old.bin"));File.Move(replacement,file);
    Pass("ReplacementFileIdRejected",()=>{using var lease=WindowsFileBoundary.OpenRead(root,"sample.bin");Reject(()=>lease.Validate(identity));});
    var hard=Path.Combine(root,"hard.bin");if(!Native.CreateHardLink(hard,file,IntPtr.Zero))throw new IOException("CreateHardLink:"+Marshal.GetLastWin32Error());
    Pass("HardlinkRejected",()=>Reject(()=>{using var lease=WindowsFileBoundary.OpenRead(root,"sample.bin");}));File.Delete(hard);
    var link=Path.Combine(root,"symbolic.bin");
    try{File.CreateSymbolicLink(link,file);Pass("SymbolicLinkRejected",()=>Reject(()=>{using var lease=WindowsFileBoundary.OpenRead(root,"symbolic.bin");}));}
    catch(UnauthorizedAccessException){records.Add(new{Name="SymbolicLinkRejected",Status="NOT TESTED",Reason="Runner symbolic-link privilege unavailable"});}
    var child=Path.Combine(root,"child");Directory.CreateDirectory(child);
    var junction=Path.Combine(root,"junction");
    var psi=new System.Diagnostics.ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var argument in new[]{"/c","mklink","/J",junction,child})psi.ArgumentList.Add(argument);
    using(var process=System.Diagnostics.Process.Start(psi)!){process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new IOException("Junction creation failed");}
    Pass("JunctionRejected",()=>Reject(()=>{using var pin=WindowsFileBoundary.PinDirectory(junction);}));Directory.Delete(junction);
    Pass("DeleteDispositionUsesRetainedHandle",()=>{using(var lease=WindowsFileBoundary.OpenWrite(root,"sample.bin",deletable:true)){lease.Validate();lease.DeleteOpenedFile();}if(File.Exists(file))throw new IOException("Disposition not observed");});
    Console.WriteLine(JsonSerializer.Serialize(new{Status="PASS",Scope="Actual Windows APIs in disposable runner; no independent standard-user token",RestrictedToken="NOT TESTED",Tests=records}));return 0;
}
catch(Exception ex){Console.WriteLine(JsonSerializer.Serialize(new{Status="FAIL",Error=ex.GetType().Name+":"+ex.Message,Tests=records}));return 10;}
static class Native
{
    [DllImport("kernel32.dll",EntryPoint="CreateHardLinkW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]public static extern bool CreateHardLink(string link,string target,IntPtr security);
}
