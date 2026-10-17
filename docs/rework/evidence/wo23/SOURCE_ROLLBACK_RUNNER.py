from pathlib import Path
import subprocess,shutil,uuid,os,json,hashlib
R=Path.cwd();E=R/'docs/rework/evidence/wo23';E.mkdir(parents=True,exist_ok=True)
B='db8defce0764dd2421ceb78b9e3ce6b851fa9e6b';name='WindowsFileBoundary.cs'
original=subprocess.check_output(['git','show',B+':src/SmartPerformanceDoctor.UpdateEngine/'+name],cwd=R)
modified=(R/'src/SmartPerformanceDoctor.UpdateEngine'/name).read_bytes()
(E/'ORIGINAL_FILE').write_bytes(original);(E/'MODIFIED_FILE').write_bytes(modified)
(E/'DIFF_FILE').write_bytes(subprocess.check_output(['git','diff',B,'--','src/SmartPerformanceDoctor.UpdateEngine/'+name],cwd=R))
root=R.parent.parent/('wo23-rollback-'+uuid.uuid4().hex);root.mkdir();env=os.environ.copy();env['RUNNER_TEMP']=str(root)
sha=lambda x:hashlib.sha256(x).hexdigest()
script='''#!/usr/bin/env bash
set -euo pipefail
: "${RESTORE_COPY:?set independent copy path}"
target="$(realpath "$RESTORE_COPY")"
case "$target" in */wo23-rollback-*/Rollback-engine/WindowsFileBoundary.cs) ;; *) echo INDEPENDENT_COPY_REQUIRED; exit 2;; esac
here="$(cd "$(dirname "$0")" && pwd)"
printf '%s  %s\\n' 'ORIGINAL_HASH' "$here/ORIGINAL_FILE" | sha256sum -c - >/dev/null
test ! -L "$RESTORE_COPY"
cp "$here/ORIGINAL_FILE" "$target"
printf '%s  %s\\n' 'ORIGINAL_HASH' "$RESTORE_COPY" | sha256sum -c - >/dev/null
echo SOURCE_COPY_RESTORED
'''.replace('ORIGINAL_HASH',sha(original))
(E/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n')
results=[]
for mode,data in [('Baseline',original),('Modified',modified),('Rollback',modified)]:
 engine=root/(mode+'-engine');engine.mkdir()
 for p in (R/'src/SmartPerformanceDoctor.UpdateEngine').iterdir():
  if p.suffix in ('.cs','.csproj'):shutil.copyfile(p,engine/p.name)
 (engine/name).write_bytes(data)
 if mode=='Rollback':
  target=os.path.relpath(engine/name,R).replace('\\','/')
  rollbackEnv=env.copy();rollbackEnv['RESTORE_COPY']=target
  p=subprocess.run(['C:/Program Files/Git/bin/bash.exe','docs/rework/evidence/wo23/ROLLBACK.sh'],env=rollbackEnv,capture_output=True,text=True)
  results.append({'Stage':'RollbackScript','Command':'bash docs/rework/evidence/wo23/ROLLBACK.sh','EnvironmentOverride':{'RESTORE_COPY':target},'Input':'Independent modified source copy','Output':p.stdout,'Error':p.stderr,'ExitCode':p.returncode})
  assert p.returncode==0
 tool=root/(mode+'-probe');tool.mkdir()
 (tool/'Program.cs').write_text('''using SmartPerformanceDoctor.UpdateEngine;using System.Text.Json;
var root=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"wo23-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"sample.bin"),"synthetic");
using(var f=WindowsFileBoundary.OpenRead(root,"sample.bin")){f.Validate();var start=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<100;i++)f.Validate(f.Identity);var bytes=GC.GetAllocatedBytesForCurrentThread()-start;Console.WriteLine(JsonSerializer.Serialize(new{OverBudget=bytes>=8*1024*1024,IdentityVerified=f.Identity.Length==49,Hash=f.Hash()}));}Directory.Delete(root,true);return 0;
''')
 (tool/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows10.0.26100.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="'+str(engine/'SmartPerformanceDoctor.UpdateEngine.csproj')+'" /></ItemGroup></Project>')
 command=['dotnet','run','--project',os.path.relpath(tool/'Probe.csproj',R),'-c','Release','-p:Platform=x64','--verbosity','quiet']
 p=subprocess.run(command,env=env,text=True,capture_output=True)
 assert p.returncode==0,p.stdout+p.stderr
 output=json.loads(p.stdout.strip().splitlines()[-1]);assert output['OverBudget']==(mode!='Modified');assert output['IdentityVerified']
 results.append({'Stage':mode.upper(),'Command':' '.join(command),'Input':'100 retained-handle validations; 8MiB allocation budget; synthetic bytes','Output':p.stdout.strip(),'ExitCode':p.returncode,'SourceSha256':sha((engine/name).read_bytes())})
assert (R/'src/SmartPerformanceDoctor.UpdateEngine'/name).read_bytes()==modified
(E/'SOURCE_ROLLBACK_RESULT.json').write_text(json.dumps({'BaselineSHA':B,'OriginalSha256':sha(original),'ModifiedSha256':sha(modified),'Results':results,'RestoredStatus':'Original allocation behavior restored on independent source copy; repository and MODIFIED_FILE remain modified.','DataRecovery':'NOT performed by this source rollback'},indent=2))
print(json.dumps(results))
