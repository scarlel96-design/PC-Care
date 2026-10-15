using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

if(args.Length!=2)return 2;
var root=Path.GetFullPath(args[1]);
var parent=Environment.GetEnvironmentVariable("RUNNER_TEMP");
if(string.IsNullOrWhiteSpace(parent)||!root.StartsWith(Path.GetFullPath(parent).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("wo20-",StringComparison.Ordinal))return 3;
using var parentPin=WindowsFileBoundary.PinDirectory(parent);
var install=Path.Combine(root,"install");var work=Path.Combine(root,"work");var package=Path.Combine(root,"update.spdup");
static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
static void Check(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
static void Print(object o)=>Console.WriteLine(JsonSerializer.Serialize(o));
static void Add(ZipArchive zip,string name,byte[] data){using var stream=zip.CreateEntry(name,CompressionLevel.Fastest).Open();stream.Write(data);}
static void Package(string output,ECDsa key,string from,string to,Dictionary<string,byte[]> files)
{
 var m=new SignedManifest{Publisher="WO20-Fixture",KeyId="ephemeral",FromVersion=from,ToVersion=to,MinimumSupportedVersion="51.0.0",PackageId=Guid.NewGuid().ToString("N"),CreatedAt=DateTimeOffset.UtcNow.AddMinutes(-1),ExpiresAt=DateTimeOffset.UtcNow.AddHours(8),Files=files.Select(p=>new SignedFile{Path="payload/"+p.Key,Length=p.Value.Length,Sha256=Hash(p.Value)}).ToList()};
 var signature=key.SignData(SignedPackage.CanonicalBytes(m),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
 using var file=File.Create(output);using var zip=new ZipArchive(file,ZipArchiveMode.Create);
 Add(zip,SignedPackage.ManifestName,JsonSerializer.SerializeToUtf8Bytes(m));Add(zip,SignedPackage.SignatureName,JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope{Signature=Convert.ToBase64String(signature)}));
 foreach(var p in files)Add(zip,"payload/"+p.Key,p.Value);
}
void Barrier(string point){File.WriteAllText(Path.Combine(root,"checkpoint.signal"),point);Console.WriteLine("CHECKPOINT:"+point);Console.Out.Flush();Thread.Sleep(Timeout.Infinite);}
try
{
 if(args[0].StartsWith("prepare",StringComparison.Ordinal))
 {
  Check(!Directory.Exists(root),"FIXTURE-ALREADY-EXISTS");Directory.CreateDirectory(install);Directory.CreateDirectory(work);File.WriteAllText(Path.Combine(root,"fixture.marker"),"WO20_SYNTHETIC_ONLY");
  var random=args[0]=="prepare-random";var many=args[0]=="prepare-many";
  var old=new Dictionary<string,byte[]>{{"app.txt","old"u8.ToArray()},{"second.txt","old-second"u8.ToArray()}};
  var next=new Dictionary<string,byte[]>{{"app.txt","new"u8.ToArray()},{"second.txt","second"u8.ToArray()}};
  if(random){old["app.txt"]=RandomNumberGenerator.GetBytes(16*1024*1024);next["app.txt"]=RandomNumberGenerator.GetBytes(16*1024*1024);next["new.bin"]=RandomNumberGenerator.GetBytes(16*1024*1024);}
  if(many)for(var i=0;i<128;i++){old[$"small/{i}.bin"]=RandomNumberGenerator.GetBytes(4096);next[$"small/{i}.bin"]=RandomNumberGenerator.GetBytes(4096);next[$"new/{i}.bin"]=RandomNumberGenerator.GetBytes(4096);}
  foreach(var p in old){var path=Path.Combine(install,p.Key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,p.Value);}
  File.WriteAllText(Path.Combine(root,"original-hashes.json"),JsonSerializer.Serialize(old.ToDictionary(p=>p.Key,p=>Hash(p.Value))));
  using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);File.WriteAllBytes(Path.Combine(root,"public-key.bin"),key.ExportSubjectPublicKeyInfo());Package(package,key,"51.0.4","51.0.5",next);Package(Path.Combine(root,"original.spdup"),key,"51.0.3","51.0.4",old);
  var password="";
  for(var attempt=0;attempt<32;attempt++)
  {
   var secret=RandomNumberGenerator.GetBytes(32);password=Convert.ToBase64String(secret)+"aA1!";CryptographicOperations.ZeroMemory(secret);
   if(SmartPerformanceDoctor.SecurityLab.Hardening.LabPasswordPolicy.ValidateForCreate(password).IsValid)break;
  }
  Check(SmartPerformanceDoctor.SecurityLab.Hardening.LabPasswordPolicy.ValidateForCreate(password).IsValid,"FIXTURE-PASSWORD-POLICY");
  File.WriteAllBytes(Path.Combine(root,"secret.dpapi"),ProtectedData.Protect(Encoding.UTF8.GetBytes(password),null,DataProtectionScope.CurrentUser));
  using var vault=new LabVaultService(Path.Combine(install,"user-data/vault"));var created=vault.Create(password,LabKdfProfile.LabFast);Check(created.Success,"VAULT-CREATE");
  File.WriteAllBytes(Path.Combine(root,"codes.dpapi"),ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(created.RecoveryCodes),null,DataProtectionScope.CurrentUser));Check(vault.Unlock(password).Success,"VAULT-UNLOCK");
  var originals=new Dictionary<string,string>();foreach(var size in new[]{0,4096,1024*1024,4*1024*1024}){var data=RandomNumberGenerator.GetBytes(size);var name="sample-"+size+".bin";originals[name]=Hash(data);Check(vault.ImportBytes(name,name,data).Success,"VAULT-IMPORT");CryptographicOperations.ZeroMemory(data);}
  File.WriteAllText(Path.Combine(root,"vault-hashes.json"),JsonSerializer.Serialize(originals));vault.Lock();Print(new{Status="PREPARED",LogicalBytes=next.Values.Sum(b=>(long)b.Length),PackageBytes=new FileInfo(package).Length,ExistingBytes=old.Values.Sum(b=>(long)b.Length)});return 0;
 }
 Check(File.ReadAllText(Path.Combine(root,"fixture.marker"))=="WO20_SYNTHETIC_ONLY","FIXTURE-MARKER");using var installPin=WindowsFileBoundary.PinDirectory(install);
 var policy=new PublisherTrustPolicy([new TrustedPublisherKey("WO20-Fixture","ephemeral","stable",File.ReadAllBytes(Path.Combine(root,"public-key.bin")))]);
 var tx=new UpdateTransaction(policy,work,false){PerformanceProfile=new()};
 if(args[0]=="password-policy-check")
 {
  var secret=ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(root,"secret.dpapi")),null,DataProtectionScope.CurrentUser);
  var validation=SmartPerformanceDoctor.SecurityLab.Hardening.LabPasswordPolicy.ValidateForCreate(Encoding.UTF8.GetString(secret));CryptographicOperations.ZeroMemory(secret);
  Print(new{validation.IsValid,validation.Issues});return 0;
 }
 if(args[0] is "vault-check" or "vault-recovery-check")
 {
  var bytes=ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(root,"secret.dpapi")),null,DataProtectionScope.CurrentUser);var password=Encoding.UTF8.GetString(bytes);CryptographicOperations.ZeroMemory(bytes);
  using var vault=new LabVaultService(Path.Combine(install,"user-data/vault"));if(args[0]=="vault-recovery-check")
  {
   var protectedCodes=ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(root,"codes.dpapi")),null,DataProtectionScope.CurrentUser);
   var codes=JsonSerializer.Deserialize<string[]>(protectedCodes)!;CryptographicOperations.ZeroMemory(protectedCodes);
   var remaining=vault.RecoveryCodesRemaining();Check(remaining>0,"RECOVERY-SLOTS-EMPTY");
   Check(vault.UnlockWithRecoveryCode(codes[codes.Length-remaining]).Success,"VAULT-RECOVERY-REOPEN");
  }
  else Check(vault.Unlock(password).Success,"VAULT-REOPEN");
  var expected=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"vault-hashes.json")))!;var actual=new Dictionary<string,string>();var output=Path.Combine(root,"exports",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
  foreach(var entry in vault.List()){Check(vault.ExportEntry(entry.EntryId,output,true).Success,"VAULT-EXTRACT");var file=Path.Combine(output,entry.RelativePath);Check(File.Exists(file),"VAULT-PATH");using var stream=File.OpenRead(file);actual[entry.RelativePath]=Convert.ToHexString(SHA256.HashData(stream));}
  var format=vault.AuthenticatedFormat;var remainingSlots=vault.RecoveryCodesRemaining();
  Check(actual.Count==expected.Count&&expected.All(p=>actual.GetValueOrDefault(p.Key)==p.Value),"VAULT-BYTES");vault.Lock();
  Print(new{Status="BYTES_VERIFIED",Format=format,RecoverySlotsRemaining=remainingSlots,RecoveryCodeUsed=args[0]=="vault-recovery-check",Files=actual.Count,OriginalHashes=expected,ExtractedHashes=actual,Reboot="NOT TESTED"});return 0;
 }
 if(args[0]=="legacy-prepare")
 {
  Check(tx.Apply(package,install,"51.0.4").Stage==UpdateStage.Committed,"APPLY");var path=Directory.GetFiles(work,"journal.json",SearchOption.AllDirectories).Single();var j=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
  Check(j.Files.All(f=>f.Existed),"LEGACY-EXISTING-ONLY");j.SchemaVersion=1;j.Stage=UpdateStage.Applying;j.InstallIdentity=j.TransactionIdentity="";foreach(var f in j.Files)f.OriginalIdentity=f.BackupIdentity=f.InstalledIdentity="";File.WriteAllText(path,JsonSerializer.Serialize(j));
  var plan=tx.PlanLegacyRecovery(install,j.TransactionId,Path.Combine(root,"original.spdup"),"51.0.3");File.WriteAllText(Path.Combine(root,"plan.json"),JsonSerializer.Serialize(plan));Print(new{Status="PLAN_READY",Targets=plan.Targets.Count,CanExecute=plan.CanExecute});return 0;
 }
 if(args[0]=="attestation-mismatch")
 {
  File.AppendAllText(Path.Combine(install,"app.txt"),"tampered-fixture");
  var state=tx.InspectInstallationTrust(install);Check(!state.Verified&&state.RequiresReview,"ATTESTATION-FAIL-OPEN");Print(state);return 0;
 }
 if(args[0]=="enroll")
 {var state=tx.EnrollInstallation(install,Path.Combine(root,"original.spdup"),"51.0.3",true);Check(state.Verified,"ENROLL");Check(tx.InspectInstallationTrust(install).Verified,"ENROLL-REOPEN");Print(state);return 0;}
 if(args[0]=="legacy-request")
 {
  var plan=JsonSerializer.Deserialize<LegacyRecoveryPlan>(File.ReadAllText(Path.Combine(root,"plan.json")))!;
  var request=new LegacyRestoreRequest(plan,Path.Combine(root,"original.spdup"),"51.0.3"){Approval=new(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,true)};
  File.WriteAllText(Path.Combine(root,"request.json"),JsonSerializer.Serialize(request));Print(new{Status="REQUEST_PREPARED"});return 0;
 }
 if(args[0]=="legacy-unknown")
 {
  var plan=JsonSerializer.Deserialize<LegacyRecoveryPlan>(File.ReadAllText(Path.Combine(root,"plan.json")))!;
  var current=Path.Combine(install,"app.txt");File.WriteAllText(current,"uncertain synthetic partial content");var before=Hash(File.ReadAllBytes(current));
  var fresh=tx.PlanLegacyRecovery(install,plan.TransactionId,Path.Combine(root,"original.spdup"),"51.0.3");
  var result=tx.ExecuteLegacyRecovery(install,fresh,Path.Combine(root,"original.spdup"),"51.0.3",fresh.ConsentBinding,true);
  Check(result.Stage==UpdateStage.RecoveryRequired&&Hash(File.ReadAllBytes(current))==before,"UNKNOWN-CONTENT-MUTATED");Print(new{Status="RecoveryReviewRequired",Before=before,After=Hash(File.ReadAllBytes(current)),ContentStates=fresh.Targets.Select(t=>t.CurrentContentState)});return 0;
 }
 if(args[0].StartsWith("approved",StringComparison.Ordinal))
 {
  var request=Path.Combine(root,"request.json");var point=args[0].StartsWith("approved-kill:")?args[0][14..]:"";
  var result=tx.ExecuteLegacyRecoveryRequest(install,request,Hash(File.ReadAllBytes(request)),_=>true,p=>{if(p.ToString()==point)Barrier(point);});
  Print(new{Status=result.Stage.ToString(),result.Code});return result.Stage==UpdateStage.Restored?0:10;
 }
 if(args[0].StartsWith("legacy",StringComparison.Ordinal))
 {
  var plan=JsonSerializer.Deserialize<LegacyRecoveryPlan>(File.ReadAllText(Path.Combine(root,"plan.json")))!;var point=args[0].StartsWith("legacy-kill:",StringComparison.Ordinal)?args[0][12..]:"";var count=0;
  var result=tx.ExecuteLegacyRecovery(install,plan,Path.Combine(root,"original.spdup"),"51.0.3",plan.ConsentBinding,true,p=>{if(p.ToString()==point||(point=="SecondFileRestore"&&p==LegacyRestoreFaultPoint.AfterFileRestore&&++count==2))Barrier(point);});Print(new{Status=result.Stage.ToString(),result.Code});return result.Stage==UpdateStage.Restored?0:10;
 }
 if(args[0]=="verify-original")
 {
  var expected=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"original-hashes.json")))!;
  Check(expected.All(p=>Hash(File.ReadAllBytes(Path.Combine(install,p.Key)))==p.Value),"INSTALLATION-BYTES");Print(new{Status="ORIGINAL_BYTES_VERIFIED",Files=expected.Count});return 0;
 }
 var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var sw=Stopwatch.StartNew();TransactionResult result2;
 if(args[0]=="recover")result2=tx.Recover(install);
 else if(args[0]=="apply"||args[0]=="apply-kill")result2=tx.Apply(package,install,"51.0.4",faultInjector:p=>{if(args[0]=="apply-kill"&&p==UpdateFaultPoint.AfterFirstReplacement)Barrier("AfterFirstReplacement");});
 else return 2;
 sw.Stop();process.Refresh();Print(new{Status=result2.Stage.ToString(),result2.Code,ElapsedMs=sw.Elapsed.TotalMilliseconds,CpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,PeakWorkingSetBytes=process.PeakWorkingSet64,Attestation=tx.InspectInstallation(install),Phases=tx.PerformanceProfile.Snapshot()});
 return result2.Stage is UpdateStage.Committed or UpdateStage.Restored?0:10;
}
catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+":"+ex.Message);return 11;}
