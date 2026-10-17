using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
if(args.Length!=2)return 2;
var verb=args[0];var root=Path.GetFullPath(args[1]);var parent=Environment.GetEnvironmentVariable("RUNNER_TEMP");
if(string.IsNullOrWhiteSpace(parent)||!root.StartsWith(Path.GetFullPath(parent).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("wo25-"))return 3;
string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
void Print(object value)=>Console.WriteLine(JsonSerializer.Serialize(value));
if(verb is "init" or "cross-init" or "closure-init")
{
 if(Directory.Exists(root))return 3;Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"fixture.marker"),"WO25_SYNTHETIC_ONLY");
 using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);File.WriteAllBytes(Path.Combine(root,"public.bin"),key.ExportSubjectPublicKeyInfo());
 if(verb=="closure-init")
 {
  var input=Path.Combine(root,"inputs");Directory.CreateDirectory(input);File.WriteAllText(Path.Combine(input,"runtime.json"),"{}");File.WriteAllText(Path.Combine(input,"payload.dat"),"synthetic");
  var files=Directory.GetFiles(input).Select(p=>new DeploymentInputFile(Path.GetFileName(p),new FileInfo(p).Length,Hash(File.ReadAllBytes(p)),false)).ToArray();
  var raw=JsonSerializer.SerializeToUtf8Bytes(new DeploymentInputManifest("pccare-test-deployment-v1","1.0.0",new string('A',40),files));
  File.WriteAllBytes(Path.Combine(root,"manifest.json"),raw);File.WriteAllBytes(Path.Combine(root,"signature.bin"),key.SignData(raw,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
  Print(new{Status="PREPARED",KeyHash=Hash(key.ExportSubjectPublicKeyInfo())});return 0;
 }
 var install=Path.Combine(root,"install");
 if(verb=="cross-init")
 {
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT")!="github-hosted")return 3;
  install=Path.Combine(Environment.GetEnvironmentVariable("SystemDrive")+"\\","wo25-volume-"+Guid.NewGuid().ToString("N"));
  if(Directory.Exists(install))return 3;File.WriteAllText(Path.Combine(root,"peer.txt"),install);
 }
 Directory.CreateDirectory(install);Directory.CreateDirectory(Path.Combine(root,"work"));File.WriteAllText(Path.Combine(install,"fixture.marker"),"WO25_SYNTHETIC_ONLY");
 File.WriteAllText(Path.Combine(install,"app.txt"),"original bytes");
 var old=Hash(File.ReadAllBytes(Path.Combine(install,"app.txt")));File.WriteAllText(Path.Combine(root,"old-hash.txt"),old);
 var payload=new Dictionary<string,byte[]>{{"app.txt","updated bytes"u8.ToArray()},{"new.bin",RandomNumberGenerator.GetBytes(4096)}};
 var manifest=new SignedManifest{Publisher="WO25-Fixture",KeyId="ephemeral",PackageId=Guid.NewGuid().ToString("N"),FromVersion="51.0.4",ToVersion="51.0.5",MinimumSupportedVersion="51.0.0",CreatedAt=DateTimeOffset.UtcNow.AddMinutes(-1),ExpiresAt=DateTimeOffset.UtcNow.AddHours(2),Files=payload.Select(x=>new SignedFile{Path="payload/"+x.Key,Length=x.Value.Length,Sha256=Hash(x.Value)}).ToList()};
 using(var zip=new ZipArchive(File.Create(Path.Combine(root,"update.spdup")),ZipArchiveMode.Create))
 {
  void Add(string name,byte[] bytes){using var output=zip.CreateEntry(name).Open();output.Write(bytes);}
  Add(SignedPackage.ManifestName,JsonSerializer.SerializeToUtf8Bytes(manifest));Add(SignedPackage.SignatureName,JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope{Signature=Convert.ToBase64String(key.SignData(SignedPackage.CanonicalBytes(manifest),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))}));
  foreach(var item in payload)Add("payload/"+item.Key,item.Value);
 }
 using var a=WindowsFileBoundary.PinDirectory(install);using var b=WindowsFileBoundary.PinDirectory(Path.Combine(root,"work"));
 Print(new{Status="PREPARED",InstallVolume=a.Identity.Split(':')[0],StagingVolume=b.Identity.Split(':')[0],ActualDifferentVolumes=a.Identity.Split(':')[0]!=b.Identity.Split(':')[0]});return 0;
}
using var pin=WindowsFileBoundary.PinDirectory(root);if(File.ReadAllText(Path.Combine(root,"fixture.marker"))!="WO25_SYNTHETIC_ONLY")return 3;
var target=Path.Combine(root,"install");
if(File.Exists(Path.Combine(root,"peer.txt")))
{
 target=File.ReadAllText(Path.Combine(root,"peer.txt"));var expectedPrefix=Environment.GetEnvironmentVariable("SystemDrive")+"\\wo25-volume-";
 if(!target.StartsWith(expectedPrefix,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(target[expectedPrefix.Length..],"N",out _))return 3;
}
using var installPin=WindowsFileBoundary.PinDirectory(target);if(File.ReadAllText(Path.Combine(target,"fixture.marker"))!="WO25_SYNTHETIC_ONLY")return 3;
var engine=new UpdateTransaction(new PublisherTrustPolicy([new TrustedPublisherKey("WO25-Fixture","ephemeral","stable",File.ReadAllBytes(Path.Combine(root,"public.bin")))]),Path.Combine(root,"work"),false);
if(verb.StartsWith("hold:"))
{
 var mode=verb[5..];using var handle=new FileStream(Path.Combine(target,"app.txt"),FileMode.Open,mode=="write"?FileAccess.Write:FileAccess.Read,mode=="exclusive"?FileShare.None:FileShare.Read);
 File.WriteAllText(Path.Combine(root,"ready.signal"),"READY");Console.WriteLine("READY");Console.Out.Flush();Thread.Sleep(Timeout.Infinite);return 0;
}
if(verb=="check")
{
 var actual=Hash(File.ReadAllBytes(Path.Combine(target,"app.txt")));var expected=File.ReadAllText(Path.Combine(root,"old-hash.txt"));
 var absent=!File.Exists(Path.Combine(target,"new.bin"));var recovery=engine.InspectRecovery(target);
 Print(new{Status=actual==expected&&absent?"ORIGINAL_BYTES_VERIFIED":"REVIEW_REQUIRED",OriginalHash=expected,ActualHash=actual,NewFileAbsent=absent,Transactions=Directory.GetDirectories(Path.Combine(root,"work"),"tx-*").Length,Recovery=recovery});return actual==expected&&absent?0:10;
}
if(verb=="recover"){var result=engine.Recover(target);Print(result);return result.Stage==UpdateStage.Restored?0:10;}
if(verb=="history"){Print(new{History=engine.InspectInstallationHistory(target),Startup=engine.InspectStartupTrust(target)});return 0;}
if(verb=="readonly")File.SetAttributes(Path.Combine(target,"app.txt"),FileAttributes.ReadOnly);
try
{
 var watch=Stopwatch.StartNew();var result=engine.Apply(Path.Combine(root,"update.spdup"),target,"51.0.4",faultInjector:p=>
 {
  if(verb=="kill:"+p){File.WriteAllText(Path.Combine(root,"ready.signal"),p.ToString());Console.WriteLine("CHECKPOINT:"+p);Console.Out.Flush();Thread.Sleep(Timeout.Infinite);}
 });
 Print(new{result.Code,Stage=result.Stage.ToString(),result.Diagnostic,result.AnyMutationStarted,ElapsedMs=watch.Elapsed.TotalMilliseconds,Startup=engine.InspectStartupTrust(target)});return 0;
}
finally{if(verb=="readonly")File.SetAttributes(Path.Combine(target,"app.txt"),FileAttributes.Normal);}
