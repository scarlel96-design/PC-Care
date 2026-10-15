using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

if(args.Length==0)return 2;
var legacy=args[0]=="--legacy-restore";
var enroll=args[0]=="--enroll";
if(legacy||enroll ? args.Length!=5 : args.Length!=4||args[0]!="--transition")return 2;
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
        if(enroll)
        {
            using var package=WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(args[2]))!,Path.GetFileName(args[2]));
            var declaration=SignedPackage.Verify(package.Stream,args[3],BuildPublisherTrust.Current);
            if(!declaration.CanApply||declaration.Manifest is null)return 10;
            if(Console.IsInputRedirected||Console.IsOutputRedirected)return 12;
            Console.WriteLine("서명된 설치 선언 등록: 버전 "+declaration.Manifest.ToVersion+" / 파일 "+declaration.Manifest.Files.Count+"개");
            Console.WriteLine("패키지 SHA-256: "+package.Hash());
            Console.WriteLine("기존 설치 파일의 신원과 전체 해시가 일치할 때만 등록합니다. 승인하려면 ENROLL 을 입력하세요.");
            if(Console.ReadLine()!="ENROLL")return 12;
            try{if(!Process.GetProcessById(caller).WaitForExit(TimeSpan.FromSeconds(90)))return 12;}catch(ArgumentException){}
            package.Validate();
            var result=new UpdateTransaction(BuildPublisherTrust.Current,UpdateBootstrap.WorkRoot,true).EnrollInstallation(install,args[2],args[3],true);
            Console.WriteLine("INSTALLATION-ENROLLMENT:"+result.Code);return result.Verified?0:10;
        }
        if(legacy)
        {
            using var file=WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(args[2]))!,Path.GetFileName(args[2]));
            if(file.Stream.Length>1024*1024)throw new InvalidDataException("LEGACY-REQUEST-LIMIT");
            var request=JsonSerializer.Deserialize<LegacyRestoreRequest>(file.Stream,new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow})!;
            if(file.Hash()!=args[3])throw new InvalidDataException("LEGACY-REQUEST-CHANGED");
            try{if(!Process.GetProcessById(caller).WaitForExit(TimeSpan.FromSeconds(90)))return 12;}catch(ArgumentException){}
            file.Validate();
            var result=new UpdateTransaction(BuildPublisherTrust.Current,UpdateBootstrap.WorkRoot,true)
                .ExecuteLegacyRecoveryRequest(install,args[2],args[3],plan=>
                {
                    if(Console.IsInputRedirected||Console.IsOutputRedirected)return false;
                    Console.WriteLine("관리자 복원 승인: "+plan.TransactionId+" / 파일 "+plan.Targets.Count+"개");
                    Console.WriteLine("검증된 이전 버전: "+plan.OriginalVersion+". 기존 백업과 구형 저널은 보존됩니다.");
                    foreach(var target in plan.Targets)Console.WriteLine(target.Path+" / "+target.OriginalLength+" bytes");
                    Console.WriteLine("복원을 승인하려면 RESTORE "+plan.TransactionId+" 를 입력하세요.");
                    return Console.ReadLine()=="RESTORE "+plan.TransactionId;
                });
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
