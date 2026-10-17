from pathlib import Path
import os,subprocess,json,hashlib,uuid,shutil,statistics
R=Path.cwd();W=R.parent.parent;B='db8defce0764dd2421ceb78b9e3ce6b851fa9e6b'
root=W/('wo23-profile-'+uuid.uuid4().hex);root.mkdir();env=os.environ.copy();env['RUNNER_TEMP']=str(root)
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
 variants[mode]={'Source':B if mode=='Baseline' else 'Workspace input hashes below','MeasurementPatch':'Same synchronous per-phase allocation scopes; baseline Check scope and profile contract only','BuildCommand':'dotnet build <SOURCE>/Probe.csproj -c Release -p:Platform=x64 -o <BINARY> -v:minimal','BuildOutput':build.stdout.replace(str(W),'<WORKSPACE>'),'ExitCode':build.returncode,'Inputs':{x.name:sha(x) for x in engine.iterdir() if x.is_file()},'ProbeHash':sha(tool/'Program.cs')}
seed=root/'wo20-seed';prepare=run([R/'tools/SmartPerformanceDoctor.WO20Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO20Probe.exe','prepare-many',seed])
results=[]
for trial in range(1,5):
 for mode in (['Baseline','Modified'] if trial%2 else ['Modified','Baseline']):
  target=root/('wo20-'+mode.lower()+'-'+str(trial));shutil.copytree(seed,target)
  args=[root/(mode+'-binary')/'Probe.exe',target]+(['off'] if trial==4 else [])
  result=run(args);data=json.loads(result.stdout);assert data['Verified'] and data['Code']=='COMMITTED'
  if trial!=4:
   phases={x['Name']:x for x in data['Phases']};assert phases['JournalFlush']['Calls']==779
  journalPath=list((target/'work').glob('tx-*/journal.json'))[0];journal=json.loads(journalPath.read_text(encoding='utf-8-sig'))
  expected={x['Path'][8:]:x['NewSha256'] for x in journal['Files']};actual={p:sha(target/'install'/p).upper() for p in expected};assert expected==actual
  results.append({'Variant':mode,'Trial':trial,'Command':'Probe.exe <SYNTHETIC_ROOT>'+(' off' if trial==4 else ''),'Input':'Same 258-file random package','PackageHash':sha(seed/'update.spdup'),'ExitCode':result.returncode,'Output':data,'JournalBytes':journalPath.stat().st_size,'VerifiedFileHashes':actual})
fields=['ElapsedMs','CpuMs','AllocatedBytes','PeakWorkingSetBytes']
record={'Source':B,'Environment':'Local Windows synthetic filesystem; strictAcl=false; no independent standard-user token','Variants':variants,'SeedCommand':'WO20Probe prepare-many <SYNTHETIC_ROOT>','SeedOutput':prepare.stdout,'SeedExitCode':prepare.returncode,'Trials':results,'Medians':{m:{f:statistics.median(x['Output'][f] for x in results if x['Variant']==m and x['Trial']<=3) for f in fields} for m in variants},'Limitations':['Nested phases overlap; never add their allocation totals.','Phase allocation uses synchronous same-thread counters.','Trial4 disables profiling; single overhead control is not a precise overhead estimate.','PeakWorkingSet includes process startup; LOH is last completed GC, not a peak.','No strict ACL performance or low-end hardware claim.']}
(R/'artifacts/wo23-memory-profile.json').write_text(json.dumps(record,indent=2));print(json.dumps(record['Medians']))
