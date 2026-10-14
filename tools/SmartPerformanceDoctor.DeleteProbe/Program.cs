using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.App.Models.Commercial;
using SmartPerformanceDoctor.UpdateEngine;
using SmartPerformanceDoctor.RecoverySandbox;

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static byte[] Protect(byte[] data) => ProtectedData.Protect(data, "WO18-SYNTHETIC"u8.ToArray(), DataProtectionScope.CurrentUser);
static byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, "WO18-SYNTHETIC"u8.ToArray(), DataProtectionScope.CurrentUser);
try
{
    if(args is ["quarantine",var selectedParent])
    {
        var transaction=SandboxRecovery.CreateSelectedSynthetic(selectedParent,"selected.tmp",1048576);
        var source=Path.Combine(transaction.Root,"selected.tmp");var before=Hash(source);
        var consent=transaction.Approve();var availableBefore=new DriveInfo(Path.GetPathRoot(source)!).AvailableFreeSpace;
        var actionTimer=Stopwatch.StartNew();var executed=transaction.Execute(consent);
        var measurement=transaction.MeasureQuarantine(availableBefore,actionTimer.Elapsed.TotalMilliseconds);
        var restored=SandboxRecovery.Open(transaction.Root).Recover();var after=Hash(source);
        var repeated=SandboxRecovery.Open(transaction.Root).Recover();
        if(!repeated.BytesVerified||repeated.Mutations!=0||Hash(source)!=after)throw new IOException("RepeatedRestoreChangedBytes");
        if(before!=after||!restored.BytesVerified)throw new IOException("SelectedRoundTripMismatch");
        var collision=SandboxRecovery.CreateSelectedSynthetic(selectedParent,"collision.tmp");collision.Execute(collision.Approve());
        var conflict=Path.Combine(collision.Root,"collision.tmp");File.WriteAllBytes(conflict,RandomNumberGenerator.GetBytes(4096));
        var conflictHash=Hash(conflict);var blocked=false;
        try{collision.Recover();}catch(IOException){blocked=true;}
        if(!blocked||Hash(conflict)!=conflictHash)throw new IOException("RestoreCollisionOverwritten");
        Console.WriteLine(JsonSerializer.Serialize(new{Quarantine=new{BeforeHash=before,AfterHash=after,executed,restored,repeated,measurement},
            RestoreCollision=new{Blocked=blocked,ExistingBytesPreserved=true,QuarantineRetained=File.Exists(Path.Combine(collision.Root,"fixture.quarantine"))},ProductionExecutionEnabled=false}));
        return 0;
    }
    if (args is ["create", var parent])
    {
        using var parentPin=WindowsFileBoundary.PinDirectory(parent);
        var root=Path.Combine(parentPin.Root,"wo18-delete-"+Guid.NewGuid().ToString("N"));
        SecureDeleteAuditChain.PrepareDirectory(root);
        var target=Path.Combine(root,"input.tmp"); var other=Path.Combine(root,"other.bin");
        File.WriteAllBytes(target,RandomNumberGenerator.GetBytes(1048576));
        File.WriteAllBytes(other,RandomNumberGenerator.GetBytes(4096));
        var plan=new ProfessionalSecureDeleteService(Path.Combine(root,"audit-output")).PlanDryRun([target]);
        using var pin=WindowsFileBoundary.PinDirectory(root);
        using var selected=WindowsFileBoundary.OpenRead(root,"input.tmp");
        var fixture=new Fixture(pin.Identity,Hash(target),Hash(other),plan,selected.Identity);
        File.WriteAllBytes(Path.Combine(root,"fixture.dpapi"),Protect(JsonSerializer.SerializeToUtf8Bytes(fixture)));
        Console.WriteLine(root); return 0;
    }
    if (args.Length is < 2 or > 3) return 2;
    var directory=Path.GetFullPath(args[1]);
    var name=Path.GetFileName(directory);
    if (!name.StartsWith("wo18-delete-",StringComparison.Ordinal) || !Guid.TryParseExact(name[12..],"N",out _))
        throw new InvalidDataException("SyntheticRootRequired");
    using var rootPin=WindowsFileBoundary.PinDirectory(directory);
    var config=JsonSerializer.Deserialize<Fixture>(Unprotect(File.ReadAllBytes(Path.Combine(directory,"fixture.dpapi"))))
        ?? throw new InvalidDataException("SyntheticFixtureMissing");
    if (config.RootId!=rootPin.Identity || config.Plan.Targets.Count!=1
        || config.Plan.Targets[0].Path!=Path.Combine(directory,"input.tmp")) throw new InvalidDataException("SyntheticIdentityMismatch");
    void Checkpoint(string state)
    {
        if (args.Length!=3) return;
        if (args[2]=="Fault:"+state) throw new IOException("SyntheticFault:"+state);
        if (args[2]!=state) return;
        using var marker=new FileStream(Path.Combine(directory,"checkpoint.txt"),FileMode.Create,FileAccess.Write,FileShare.Read);
        marker.Write(Encoding.UTF8.GetBytes(state)); marker.Flush(true);
        Thread.Sleep(Timeout.Infinite);
    }
    var timer=Stopwatch.StartNew(); using var process=Process.GetCurrentProcess(); var cpu=process.TotalProcessorTime;
    object result;
    var audit=Path.Combine(directory,"audit-output","audit");
    if(args[0]=="binding")
    {
        var key=SecureDeleteAuditChain.LoadOperationKey(config.Plan.OperationId,audit);
        try
        {
            var records=SecureDeleteJournal.Read(audit,config.Plan.OperationId,key);
            var bound=records.Single(r=>r.State=="TargetHandleBound");
            var content=SecureDeleteAuditChain.ProtectContentFingerprint(config.OriginalHash,key);
            result=new{Authenticated=true,RecordCount=records.Count,States=records.Select(r=>r.State),
                ContentFingerprintMatches=content==bound.ContentFingerprint,SelectedFileIdMatches=config.TargetId==bound.FileIdentity,
                VolumeIdPresent=bound.VolumeId.Length>0,OwnerBound=bound.OwnerFingerprint.Length==64};
        }
        finally{CryptographicOperations.ZeroMemory(key);}
    }
    else if (args[0]=="execute")
    {
        var service=new ProfessionalSecureDeleteService(Path.Combine(directory,"audit-output")){TestCheckpoint=Checkpoint};
        result=await service.ApplyDetailedAsync(config.Plan,"보안 삭제에 동의합니다");
    }
    else if (args[0]=="reconcile") result=SecureDeleteJournal.Reconcile(audit,config.Plan.OperationId,Checkpoint);
    else throw new ArgumentException("UnsupportedCommand");
    var input=Path.Combine(directory,"input.tmp");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Result=result,InputPresent=File.Exists(input),OriginalContentMatches=File.Exists(input)&&Hash(input)==config.OriginalHash,
        OtherContentMatches=Hash(Path.Combine(directory,"other.bin"))==config.OtherHash,
        ElapsedMs=timer.Elapsed.TotalMilliseconds,CpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,
        WorkingSetBytes=process.WorkingSet64
    }).Replace(directory.Replace("\\", "\\\\"), "<SYNTHETIC_ROOT>").Replace(directory, "<SYNTHETIC_ROOT>"));
    return 0;
}
catch(Exception error){Console.Error.WriteLine("WO18_PROBE_BLOCKED "+error.GetType().Name+":"+error.Message);return 3;}
internal sealed record Fixture(string RootId,string OriginalHash,string OtherHash,SecureDeletePlan Plan,string TargetId="");
