using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

// Disposable runner experiment. Inspection never publishes, resumes, or removes a file.
internal static class Stage29
{
    internal static int Run(string verb, string root, string parent)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted") return 3;
        var name = Path.GetFileName(root);
        if (!name.StartsWith("wo27-stage29-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[13..], "N", out _) ||
            !string.Equals(Path.GetDirectoryName(root), Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)), StringComparison.OrdinalIgnoreCase)) return 3;
        var args = verb.Split(':');
        if (args.Length != 4 || args[2] is not ("A" or "B")) return 3;
        using var control = WindowsFileBoundary.PinDirectory(root);
        var peer = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + @"\", name);
        var target = Path.Combine(peer, "install");
        var sourceRoot = args[2] == "A" ? peer : root;
        using var system = WindowsFileBoundary.PinDirectory(Path.GetPathRoot(peer)!);
        if (system.Identity.Split(':')[0] == control.Identity.Split(':')[0]) { Console.WriteLine("DISTINCT_VOLUMES_UNAVAILABLE"); return 12; }
        var sid = WindowsIdentity.GetCurrent().User!;
        string Descriptor(FileSystemSecurity acl) => acl.GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Access);
        void CheckAcl(FileSystemSecurity acl)
        {
            var owner = acl.GetOwner(typeof(SecurityIdentifier))?.Value;
            if (owner != sid.Value && owner != "S-1-5-32-544") throw new IOException("OWNER_MISMATCH");
            var allowed = new[] { sid.Value, "S-1-5-18", "S-1-5-32-544" };
            foreach (FileSystemAccessRule r in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                if (r.AccessControlType != AccessControlType.Allow || !allowed.Contains(r.IdentityReference.Value)) throw new IOException("ACL_MISMATCH");
        }
        void Emit(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
        void Stop(string phase)
        {
            if (args[3] != phase) return;
            // Publish the complete signal before parking.
            File.WriteAllText(Path.Combine(root,"signal.tmp"),phase);
            File.Move(Path.Combine(root,"signal.tmp"),Path.Combine(root,"signal"));
            Thread.Sleep(Timeout.Infinite);
        }
        object? Item(string dir,string file)
        {
            if (!File.Exists(Path.Combine(dir,file))) return null;
            using var lease = WindowsFileBoundary.OpenRead(dir,file);
            CheckAcl(lease.ReadSecurity());
            var h = lease.Hash(); lease.Validate();
            return new { Id=lease.Identity, Size=lease.Stream.Length, Hash=h,
                AclHash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Descriptor(lease.ReadSecurity())))) };
        }
        if (args[1]=="inspect")
        {
            if (!Directory.Exists(peer)) { Emit(new { State="NoPeer" }); return 0; }
            using var pin=WindowsFileBoundary.PinDirectory(peer);
            CheckAcl(new DirectoryInfo(peer).GetAccessControl());
            var source=Item(sourceRoot,"input.bin"); var temp=Item(peer,"staged.tmp"); var published=Item(target,"published.bin");
            var receipt=Item(peer,"committed.json");
            if(receipt!=null)
            {
                using var journal=WindowsFileBoundary.OpenRead(peer,"committed.json");
                using var parsed=JsonDocument.Parse(journal.Stream);
                using var dest=WindowsFileBoundary.OpenRead(target,"published.bin");
                var j=parsed.RootElement; dest.Verify(j.GetProperty("Size").GetInt64(),j.GetProperty("Hash").GetString()!,j.GetProperty("Id").GetString());
            }
            pin.Validate();control.Validate();
            Emit(new {State="Inspected",Source=source,Temp=temp,Published=published,Receipt=receipt});return 0;
        }
        if(args[1]!="run" || Directory.Exists(peer)) return 3;
        Stop("BeforeDirectory");
        var protection=new DirectorySecurity();protection.SetAccessRuleProtection(true,false);protection.SetOwner(sid);
        protection.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        new DirectoryInfo(peer).Create(protection);
        using var peerPin=WindowsFileBoundary.PinDirectory(peer);
        CheckAcl(new DirectoryInfo(peer).GetAccessControl());Stop("AfterDirectory");
        Directory.CreateDirectory(target);
        using var installPin=WindowsFileBoundary.PinDirectory(target);
        // Both sources inherit the private test directory ACL.
        var bytes=RandomNumberGenerator.GetBytes(1024*1024);
        var expected=Convert.ToHexString(SHA256.HashData(bytes));
        using(var input=WindowsFileBoundary.OpenWrite(sourceRoot,"input.bin",create:true))
        { input.Stream.Write(bytes);input.Stream.Flush(true);input.Verify(bytes.Length,expected); }
        using var sourceLease=WindowsFileBoundary.OpenRead(sourceRoot,"input.bin");CheckAcl(sourceLease.ReadSecurity());
        Emit(new { Stage="Input",Source=Item(sourceRoot,"input.bin") });Console.Out.Flush();Stop("BeforeCopy");
        using var staging=WindowsFileBoundary.OpenWrite(peer,"staged.tmp",create:true,deletable:true);
        staging.CopyFromObserved(sourceLease,p=>{
            if(p.Phase=="AfterWrite" && p.BytesWritten==4096) Stop("DuringCopy");
            if(p.Phase=="AfterLastWrite") Stop("AfterCopy");
            if(p.Phase=="BeforeFlush" || p.Phase=="AfterFlush") Stop(p.Phase);
        });
        staging.Verify(bytes.Length,expected);CheckAcl(staging.ReadSecurity());
        Emit(new { Stage="Prepared", Id=staging.Identity });Console.Out.Flush();Stop("BeforePublish");
        staging.PublishOpenedFile(target,"published.bin");staging.Verify(bytes.Length,expected);Stop("AfterPublish");
        Stop("BeforeJournal");
        using(var journal=WindowsFileBoundary.OpenWrite(peer,"committed.json",create:true))
        { JsonSerializer.Serialize(journal.Stream,new {Id=staging.Identity,Size=bytes.Length,Hash=expected});journal.Stream.Flush(true); }
        Stop("AfterJournal");return 0;
    }
}

