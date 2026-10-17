from pathlib import Path
import subprocess,json,os,uuid,hashlib,time,shutil
R=Path.cwd();B='db8defce0764dd2421ceb78b9e3ce6b851fa9e6b';root=R.parent.parent/('wo23-publication-'+uuid.uuid4().hex);root.mkdir();env=os.environ.copy();env['RUNNER_TEMP']=str(root)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
program=r'''using SmartPerformanceDoctor.UpdateEngine;using System.Text.Json;using System.Security.Cryptography;using System.IO.Compression;
var root=Path.GetFullPath(args[1]);var install=Path.Combine(root,"install");var work=Path.Combine(root,"work");
void Print(object o)=>Console.WriteLine(JsonSerializer.Serialize(o));
if(args[0]=="go"){
 Directory.CreateDirectory(install);Directory.CreateDirectory(work);using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);File.WriteAllBytes(Path.Combine(root,"public.bin"),key.ExportSubjectPublicKeyInfo());
 string Package(string from,string to,string name){var bytes=System.Text.Encoding.UTF8.GetBytes(name);var manifest=new SignedManifest{Publisher="WO23-Fixture",KeyId="ephemeral",PackageId=Guid.NewGuid().ToString("N"),FromVersion=from,ToVersion=to,MinimumSupportedVersion="51.0.0",CreatedAt=DateTimeOffset.UtcNow.AddMinutes(-1),ExpiresAt=DateTimeOffset.UtcNow.AddHours(2),Files=[new SignedFile{Path="payload/"+name,Length=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes))}]};var path=Path.Combine(root,name+".spdup");using var zip=new ZipArchive(File.Create(path),ZipArchiveMode.Create);void Add(string p,byte[] b){using var s=zip.CreateEntry(p).Open();s.Write(b);}Add(SignedPackage.ManifestName,JsonSerializer.SerializeToUtf8Bytes(manifest));Add(SignedPackage.SignatureName,JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope{Signature=Convert.ToBase64String(key.SignData(SignedPackage.CanonicalBytes(manifest),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))}));Add("payload/"+name,bytes);return path;}
 var policy=new PublisherTrustPolicy([new TrustedPublisherKey("WO23-Fixture","ephemeral","stable",key.ExportSubjectPublicKeyInfo())]);var e=new UpdateTransaction(policy,work,false);
 if(e.Apply(Package("51.0.4","51.0.5","first.bin"),install,"51.0.4").Stage!=UpdateStage.Committed)return 10;
 var next=Package("51.0.5","51.0.6","second.bin");
 e.Apply(next,install,"51.0.5",faultInjector:p=>{if(p==UpdateFaultPoint.JournalAfterReplace){var files=Directory.GetFiles(work,"journal.json",SearchOption.AllDirectories);if(files.Count(f=>JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(f))!.Stage==UpdateStage.Committed)==2){File.WriteAllText(Path.Combine(root,"checkpoint.signal"),"JournalCommittedBeforeAttestation");Thread.Sleep(Timeout.Infinite);}}});return 11;
}
var readPolicy=new PublisherTrustPolicy([new TrustedPublisherKey("WO23-Fixture","ephemeral","stable",File.ReadAllBytes(Path.Combine(root,"public.bin")))]);var engine=new UpdateTransaction(readPolicy,work,false);
Print(new{Trust=engine.InspectInstallationTrust(install),Recovery=engine.InspectRecovery(install),Hashes=Directory.GetFiles(install).ToDictionary(Path.GetFileName,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))});return 0;
'''
records=[]
for mode in ['Baseline','Modified']:
 engine=root/(mode+'-engine');engine.mkdir()
 for p in (R/'src/SmartPerformanceDoctor.UpdateEngine').iterdir():
  if p.suffix in ('.cs','.csproj'):(engine/p.name).write_bytes(subprocess.check_output(['git','show',B+':src/SmartPerformanceDoctor.UpdateEngine/'+p.name],cwd=R) if mode=='Baseline' else p.read_bytes())
 tool=root/(mode+'-probe');tool.mkdir();(tool/'Program.cs').write_text(program,encoding='utf-8')
 (tool/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows10.0.26100.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="'+str(engine/'SmartPerformanceDoctor.UpdateEngine.csproj')+'" /></ItemGroup></Project>')
 args=['dotnet','build',str(tool/'Probe.csproj'),'-c','Release','-p:Platform=x64','-o',str(root/(mode+'-binary')),'-v:minimal'];p=subprocess.run(args,cwd=R,env=env,capture_output=True,text=True,encoding="utf-8",errors="replace");assert p.returncode==0,p.stdout+p.stderr
 records.append({'Variant':mode,'Command':'dotnet build <INDEPENDENT_SOURCE>/Probe.csproj -c Release -p:Platform=x64 -o <BINARY> -v:minimal','ExitCode':p.returncode,'Inputs':{p.name:sha(p) for p in engine.iterdir() if p.is_file()}})
fixture=root/'wo23-legacy-fixture';fixture.mkdir();worker=subprocess.Popen([str(root/'Baseline-binary/Probe.exe'),'go',str(fixture)],env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=subprocess.CREATE_NO_WINDOW)
try:
 deadline=time.monotonic()+60
 while not (fixture/'checkpoint.signal').exists() and worker.poll() is None and time.monotonic()<deadline:time.sleep(.05)
 assert (fixture/'checkpoint.signal').exists(),'Legacy checkpoint not reached'
 assert worker.poll() is None
 worker.kill();worker.wait(timeout=15);killed=worker.returncode
finally:
 if worker.poll() is None:worker.kill();worker.wait()
before={str(p.relative_to(fixture)):sha(p) for p in fixture.rglob('*') if p.is_file()}
queries=[]
for mode in ['Baseline','Modified']:
 p=subprocess.run([str(root/(mode+'-binary')/'Probe.exe'),'query',str(fixture)],env=env,capture_output=True,text=True,encoding="utf-8",errors="replace");assert p.returncode==0,p.stdout+p.stderr;v=json.loads(p.stdout);queries.append({'Variant':mode,'Command':'Probe.exe query <SYNTHETIC_LEGACY_FIXTURE>','ExitCode':p.returncode,'Output':v});assert v['Trust']['Verified']==(mode=='Baseline')
assert before=={str(p.relative_to(fixture)):sha(p) for p in fixture.rglob('*') if p.is_file()}
assert queries[0]['Output']['Hashes']==queries[1]['Output']['Hashes']
result={'Status':'PASS (legacy defect reproduced; modified reader blocks it)','BaselineSecurityVerdict':'FAIL: obsolete pointer incorrectly Verified disjoint committed successor','BaselineSource':B,'ModifiedInputs':records[1]['Inputs'],'Environment':'Separate Windows processes; synthetic signed packages; strictAcl=false; no administrator/UAC/PE deployment','Checkpoint':'JournalCommittedBeforeAttestation','WorkerExitCode':killed,'WorkerCommand':'Baseline-Probe.exe go <NEW_SYNTHETIC_FIXTURE> then parent Process.kill','Queries':queries,'ReadOnlySnapshotUnchanged':True,'DataRecovery':'Not performed; both files preserved and review required','Builds':records}
(R/'artifacts/wo23-legacy-publication-gap.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'Checkpoint':result['Checkpoint'],'WorkerExitCode':killed,'BaselineVerified':True,'ModifiedVerified':False,'ReadOnlySnapshotUnchanged':True}))
