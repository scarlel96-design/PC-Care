from pathlib import Path
import os,subprocess,json,hashlib,uuid,shutil,statistics
R=Path.cwd();W=R.parent.parent;B='db8defce0764dd2421ceb78b9e3ce6b851fa9e6b'
root=W/('wo23-final-profile-'+uuid.uuid4().hex);root.mkdir();env=os.environ.copy();env['RUNNER_TEMP']=str(root)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(args):
 p=subprocess.run([str(x) for x in args],cwd=R,env=env,text=True,capture_output=True,encoding='utf-8',errors='replace')
 if p.returncode:raise RuntimeError(str(args)+p.stdout[-2000:]+p.stderr[-1500:])
 return p
variants={}
for mode in ['Baseline','Modified']:
 engine=root/(mode+'-engine');engine.mkdir()
 for p in (R/'src/SmartPerformanceDoctor.UpdateEngine').iterdir():
  if p.suffix in ('.cs','.csproj'):
   data=subprocess.check_output(['git','show',B+':src/SmartPerformanceDoctor.UpdateEngine/'+p.name],cwd=R) if mode=='Baseline' else p.read_bytes()
   (engine/p.name).write_bytes(data)
 if mode=='Baseline':
  shutil.copyfile(R/'src/SmartPerformanceDoctor.UpdateEngine/UpdatePerformanceProfile.cs',engine/'UpdatePerformanceProfile.cs')
  p=engine/'WindowsFileBoundary.cs';s=p.read_text(encoding='utf-8-sig').replace('        if (!GetFileInformationByHandle(handle, out var basic))','#if !PCCARE_VAULT_BOUNDARY\n        using var measurement=UpdatePerformanceProfile.Active?.Measure("NativeIdentityCheck");\n#endif\n        if (!GetFileInformationByHandle(handle, out var basic))');p.write_text(s)
 (root/'Directory.Build.props').write_bytes((R/'Directory.Build.props').read_bytes())
 tool=root/(mode+'-probe');tool.mkdir();shutil.copyfile(R/'tools/SmartPerformanceDoctor.UpdatePerformanceProbe/Program.cs',tool/'Program.cs')
 (tool/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows10.0.26100.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="'+str(engine/'SmartPerformanceDoctor.UpdateEngine.csproj')+'" /></ItemGroup></Project>')
 build=run(['dotnet','build',tool/'Probe.csproj','-c','Release','-p:Platform=x64','-o',root/(mode+'-binary'),'-v:minimal'])
 variants[mode]={'Source':B if mode=='Baseline' else subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip(),'MeasurementPatch':'Same synchronous per-phase allocation scopes; baseline Check scope and profile contract only','BuildCommand':'dotnet build <SOURCE>/Probe.csproj -c Release -p:Platform=x64 -o <BINARY> -v:minimal','BuildOutput':build.stdout.replace(str(W),'<WORKSPACE>'),'ExitCode':build.returncode,'Inputs':{x.name:sha(x) for x in engine.iterdir() if x.is_file()},'ProbeHash':sha(tool/'Program.cs')}
seed=root/'wo20-seed';prepare=run([R/'tools/SmartPerformanceDoctor.WO20Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO20Probe.exe','prepare-many',seed])

source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip()
results=[]
record={'Source':source,'BaselineSource':B,'Variants':variants,'Environment':'Local Windows26200; 24 logical processors; synthetic strictAcl=false; no concurrent local build or test','SeedCommand':'WO20Probe prepare-many <SYNTHETIC_ROOT>','SeedExitCode':prepare.returncode,'Trials':results,'Status':'RUNNING','Limitations':['Only successful trials enter medians; all failed attempts remain recorded.','Three profiling-disabled matched trials per variant; one profiled trial per variant.','Nested phase allocations overlap.','Peak Working Set includes process startup and post-Apply inspection.','LOH value is the last completed GC size, not a peak.','No physical power-loss, standard-user deployment, or low-end hardware qualification.']}
output=R/'artifacts/wo23-final-memory-profile.json'
def save():output.write_text(json.dumps(record,indent=2),encoding='utf-8')
save()
for trial in range(1,5):
 for mode in (['Baseline','Modified'] if trial%2 else ['Modified','Baseline']):
  target=root/('wo20-'+mode.lower()+'-'+str(trial));shutil.copytree(seed,target)
  args=[str(root/(mode+'-binary')/'Probe.exe'),str(target)]+(['off'] if trial<=3 else [])
  p=subprocess.run(args,cwd=R,env=env,text=True,capture_output=True,encoding='utf-8',errors='replace')
  try:data=json.loads(p.stdout)
  except Exception:data={'ParseError':True,'RawOutput':p.stdout}
  row={'Variant':mode,'Trial':trial,'Command':'Probe.exe <SYNTHETIC_ROOT>'+(' off' if trial<=3 else ''),'ExitCode':p.returncode,'Output':data,'Error':p.stderr,'PackageHash':sha(seed/'update.spdup')}
  results.append(row);save()
  journals=list((target/'work').glob('tx-*/journal.json'))
  if journals:
   jp=journals[0];journal=json.loads(jp.read_text(encoding='utf-8-sig'));row['JournalStage']=journal['Stage'];row['Failure']=journal['Failure'];row['JournalBytes']=jp.stat().st_size
   expected={x['Path'][8:]:x['NewSha256'] for x in journal['Files']};actual={n:sha(target/'install'/n).upper() if (target/'install'/n).exists() else None for n in expected};row['VerifiedFileHashes']=actual
   row['CommittedHashesVerified']=expected==actual and data.get('Code')=='COMMITTED' and data.get('Verified') and p.returncode==0
   if not row['CommittedHashesVerified']:
    originals=json.loads((target/'original-hashes.json').read_text(encoding='utf-8-sig'));row['RollbackOriginalHashes']={n:sha(target/'install'/n).upper() if (target/'install'/n).exists() else None for n in originals};row['RollbackOriginalVerified']=originals==row['RollbackOriginalHashes'];row['NewFilesAbsent']=all(not(target/'install'/x['Path'][8:]).exists() for x in journal['Files'] if not x['Existed'])
  save()
fields=['ElapsedMs','CpuMs','AllocatedBytes','PeakWorkingSetBytes']
record['Medians']={m:{f:statistics.median(x['Output'][f] for x in results if x['Variant']==m and x['Trial']<=3 and x.get('CommittedHashesVerified')) for f in fields} for m in variants}
record['FailedTrials']=sum(not x.get('CommittedHashesVerified',False) for x in results)
record['Status']='PASS (measurement)' if record['FailedTrials']==0 else 'PARTIAL (failed attempts retained)'
save();print(json.dumps({'Status':record['Status'],'Medians':record['Medians'],'FailedTrials':record['FailedTrials']}))
