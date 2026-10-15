from pathlib import Path
import subprocess,hashlib,shutil,json,uuid,os
W=Path(__file__).parent;R=W/'work/PC-Care';E=R/'docs/rework/evidence/wo22';base='cc0fa735a4b1faaa7d72595e131372257e87f225';source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip()
target=R/'src/SmartPerformanceDoctor.UpdateEngine/InstallationTrustPolicy.cs'
original=subprocess.check_output(['git','show',base+':src/SmartPerformanceDoctor.UpdateEngine/InstallationTrustPolicy.cs'],cwd=R);modified=target.read_bytes()
(E/'ORIGINAL_FILE').write_bytes(original);(E/'MODIFIED_FILE').write_bytes(modified)
sha=lambda b:hashlib.sha256(b).hexdigest();oldsha=sha(original);newsha=sha(modified)
(E/'rollback_source.py').write_text(f'''from pathlib import Path
import sys,hashlib
base=Path(__file__).resolve().parent
if len(sys.argv)!=2:raise SystemExit("Usage: ROLLBACK.sh ABSOLUTE_RESTORE_COPY")
p=Path(sys.argv[1])
if not p.is_absolute() or p.name!="RESTORE_COPY" or not p.parent.name.startswith("wo22-rollback-"):raise SystemExit("Independent source copy required")
for x in [p,*p.parents]:
 if x.is_symlink() or (hasattr(x,"is_junction") and x.is_junction()):raise SystemExit("Reparse boundary rejected")
if p.stat().st_nlink!=1:raise SystemExit("Hardlink rejected")
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
if sha(base/"ORIGINAL_FILE")!="{oldsha}" or sha(p)!="{newsha}":raise SystemExit("Source hash mismatch")
p.write_bytes((base/"ORIGINAL_FILE").read_bytes())
if sha(p)!="{oldsha}":raise SystemExit("Source restore failed")
print("SOURCE_COPY_RESTORED; INSTALLATION_UNCHANGED")
''',encoding='utf-8')
(E/'ROLLBACK.sh').write_bytes(b'#!/usr/bin/env bash\nset -euo pipefail\npython "$(dirname "$0")/rollback_source.py" "$@"\n')
root=W/('wo22-rollback-'+uuid.uuid4().hex);root.mkdir();env=os.environ.copy();env['RUNNER_TEMP']=str(root);records=[]
def run(name,args):
 p=subprocess.run([str(x) for x in args],cwd=R,env=env,text=True,capture_output=True,encoding='utf-8',errors='replace')
 records.append({'Name':name,'Command':[str(x).replace(str(W),'<WORKSPACE>') for x in args],'Input':'Synthetic signed enrollment, update, schema1 restore; independent source-copy engine','Output':(p.stdout+p.stderr).replace(str(W),'<WORKSPACE>'),'ExitCode':p.returncode})
 if p.returncode:raise RuntimeError(records[-1]['Output'][-1500:])
 return p.stdout
engine=root/'engine';shutil.copytree(R/'src/SmartPerformanceDoctor.UpdateEngine',engine,ignore=shutil.ignore_patterns('bin','obj'));shutil.copyfile(R/'Directory.Build.props',root/'Directory.Build.props')
probe=root/'probe';probe.mkdir();(probe/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows10.0.26100.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="../engine/SmartPerformanceDoctor.UpdateEngine.csproj" /></ItemGroup></Project>',encoding='utf-8')
(probe/'Program.cs').write_text('''using System.Text.Json;using SmartPerformanceDoctor.UpdateEngine;
var root=Path.GetFullPath(args[0]);var install=Path.Combine(root,"install");var work=Path.Combine(root,"work");
var policy=new PublisherTrustPolicy([new TrustedPublisherKey("WO20-Fixture","ephemeral","stable",File.ReadAllBytes(Path.Combine(root,"public-key.bin")))]);
var tx=new UpdateTransaction(policy,work,false);var old=Path.Combine(root,"original.spdup");
if(!tx.EnrollInstallation(install,old,"51.0.3",true).Verified)return 10;
if(tx.Apply(Path.Combine(root,"update.spdup"),install,"51.0.4").Stage!=UpdateStage.Committed)return 11;
var journalPath=Directory.GetFiles(work,"journal.json",SearchOption.AllDirectories).Single();var j=JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(journalPath))!;
j.SchemaVersion=1;j.Stage=UpdateStage.Applying;j.InstallIdentity=j.TransactionIdentity="";foreach(var e in j.Files)e.OriginalIdentity=e.BackupIdentity=e.InstalledIdentity="";
File.WriteAllText(journalPath,JsonSerializer.Serialize(j));var plan=tx.PlanLegacyRecovery(install,j.TransactionId,old,"51.0.3");
if(tx.ExecuteLegacyRecovery(install,plan,old,"51.0.3",plan.ConsentBinding,true).Stage!=UpdateStage.Restored)return 12;
Console.WriteLine(JsonSerializer.Serialize(tx.InspectInstallationTrust(install)));return 0;
''',encoding='utf-8')
try:
 for mode,data in [('BASELINE',original),('MODIFIED',modified),('ROLLBACK',original)]:
  if mode=='ROLLBACK':
   copy=root/'RESTORE_COPY';copy.write_bytes(modified)
   run('ROLLBACK-script',[r'C:\Program Files\Git\bin\bash.exe',E/'ROLLBACK.sh',copy]);assert copy.read_bytes()==original;data=copy.read_bytes()
  (engine/'InstallationTrustPolicy.cs').write_bytes(data)
  run(mode+'-build',['dotnet','build',probe/'Probe.csproj','-c','Release','-p:Platform=x64','-o',root/'binary','-v:minimal'])
  fixture=root/('wo20-'+mode.lower());run(mode+'-prepare',[R/'tools/SmartPerformanceDoctor.WO20Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO20Probe.exe','prepare',fixture])
  result=json.loads(run(mode+'-behavior',[root/'binary/Probe.exe',fixture]));expected='SIGNED-ENROLLMENT-VERIFIED' if mode=='MODIFIED' else 'RESTORED-INSTALLATION-NOT-ATTESTED'
  assert result['Code']==expected and result['Verified']==(mode=='MODIFIED')
  records.append({'Name':mode+'-assert','Command':'Assert exact installation trust result','Input':'Authenticated original enrollment after reviewed legacy restoration','Output':expected,'ExitCode':0})
 assert target.read_bytes()==modified and (E/'MODIFIED_FILE').read_bytes()==modified
finally:
 (R/'artifacts/wo22-source-rollback.json').write_text(json.dumps({'Source':source,'BaselineSource':base,'OriginalSha256':oldsha,'ModifiedSha256':newsha,'Scope':'Independent source-copy rollback only','Records':records},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'OriginalSha256':oldsha,'ModifiedSha256':newsha,'BaselineCode':'RESTORED-INSTALLATION-NOT-ATTESTED','ModifiedCode':'SIGNED-ENROLLMENT-VERIFIED','RollbackCode':'RESTORED-INSTALLATION-NOT-ATTESTED','ExitCode':0}))
