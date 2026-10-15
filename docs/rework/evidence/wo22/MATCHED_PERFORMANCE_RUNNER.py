from pathlib import Path
import os,subprocess,json,hashlib,uuid,shutil,statistics
W=Path(__file__).parent;R=W/'work/PC-Care';B='cc0fa735a4b1faaa7d72595e131372257e87f225'
S=subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip()
root=W/('wo22-profile-'+uuid.uuid4().hex);root.mkdir();env=os.environ.copy();env['RUNNER_TEMP']=str(root)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(args):
 p=subprocess.run([str(x) for x in args],cwd=R,env=env,text=True,capture_output=True,encoding='utf-8',errors='replace')
 if p.returncode:raise RuntimeError(str(args)+p.stdout[-2000:]+p.stderr[-1500:])
 return p
variants={}
for mode in ['Baseline','Modified']:
 engine=root/(mode+'-engine');engine.mkdir();ref=B if mode=='Baseline' else S
 files=subprocess.check_output(['git','ls-tree','--name-only',ref+':src/SmartPerformanceDoctor.UpdateEngine'],cwd=R,text=True).splitlines()
 for name in files:
  if name.endswith(('.cs','.csproj')):(engine/name).write_bytes(subprocess.check_output(['git','show',ref+':src/SmartPerformanceDoctor.UpdateEngine/'+name],cwd=R))
 if mode=='Baseline':
  p=engine/'WindowsFileBoundary.cs';text=p.read_text(encoding='utf-8');position=text.index('{',text.index('        internal FileLease('))+1
  text=text[:position]+'\n#if !PCCARE_VAULT_BOUNDARY\n            using var openingMeasurement=UpdatePerformanceProfile.Active?.Measure(create?"FileCreateAndBind":"FileOpenAndBind");\n#endif'+text[position:]
  text=text.replace('            Stream.Flush(flushToDisk: true);','#if !PCCARE_VAULT_BOUNDARY\n            using(UpdatePerformanceProfile.Active?.Measure("PayloadFlush")) Stream.Flush(flushToDisk: true);\n#else\n            Stream.Flush(flushToDisk: true);\n#endif');p.write_text(text,encoding='utf-8')
  p=engine/'UpdateTransaction.cs';text=p.read_text(encoding='utf-8').replace('                        output.Stream.Flush(flushToDisk: true);','                        using(PerformanceProfile?.Measure("PayloadFlush"))output.Stream.Flush(flushToDisk: true);').replace('                faultInjector?.Invoke(UpdateFaultPoint.DuringPostApplyVerification);','                using var postVerification=PerformanceProfile?.Measure("PostApplyFileVerification");\n                faultInjector?.Invoke(UpdateFaultPoint.DuringPostApplyVerification);');p.write_text(text,encoding='utf-8')
 (root/'Directory.Build.props').write_bytes(subprocess.check_output(['git','show',ref+':Directory.Build.props'],cwd=R))
 tool=root/(mode+'-probe');tool.mkdir();shutil.copyfile(R/'tools/SmartPerformanceDoctor.UpdatePerformanceProbe/Program.cs',tool/'Program.cs')
 (tool/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows10.0.26100.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="'+str(engine/'SmartPerformanceDoctor.UpdateEngine.csproj')+'" /></ItemGroup></Project>',encoding='utf-8')
 build=run(['dotnet','build',tool/'Probe.csproj','-c','Release','-p:Platform=x64','-o',root/(mode+'-binary'),'-v:minimal'])
 variants[mode]={'Source':ref,'MeasurementOnlyPatch':('Four identical opt-in scopes backported: FileCreateAndBind,FileOpenAndBind,PayloadFlush,PostApplyFileVerification' if mode=='Baseline' else 'None; committed source'),'BuildCommand':'dotnet build <VARIANT_SOURCE>/Probe.csproj -c Release -p:Platform=x64 -o <VARIANT_BINARY> -v:minimal','BuildOutput':build.stdout.replace(str(W),'<WORKSPACE>'),'ExitCode':build.returncode,'EngineInputs':{x.name:sha(x) for x in engine.iterdir() if x.is_file()},'ProbeSourceSha256':sha(tool/'Program.cs'),'EngineSha256':sha(root/(mode+'-binary')/'SmartPerformanceDoctor.UpdateEngine.dll'),'ProbeSha256':sha(root/(mode+'-binary')/'Probe.dll')}
seed=root/'wo20-seed';prepare=run([R/'tools/SmartPerformanceDoctor.WO20Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO20Probe.exe','prepare-many',seed])
results=[]
for trial in range(1,4):
 for mode in (['Baseline','Modified'] if trial%2 else ['Modified','Baseline']):
  target=root/('wo20-'+mode.lower()+'-'+str(trial));shutil.copytree(seed,target)
  result=run([root/(mode+'-binary')/'Probe.exe',target]);data=json.loads(result.stdout)
  assert data['Verified'] and data['Code']=='COMMITTED'
  phases={x['Name']:x for x in data['Phases']};assert phases['JournalFlush']['Calls']==779 and phases['JournalTotal']['Calls']==779
  records=list((target/'work').glob('tx-*/journal.json'));journal=json.loads(records[0].read_text(encoding='utf-8-sig'))
  expected={x['Path'][8:]:x['NewSha256'] for x in journal['Files']};actual={p:sha(target/'install'/p).upper() for p in expected};assert expected==actual
  results.append({'Variant':mode,'Trial':trial,'Command':'<VARIANT_BINARY>/Probe.exe <SYNTHETIC_ROOT>','Input':'Same 258-file random package; 128 existing4KiB+128new4KiB+2small','InputPackageSha256':sha(seed/'update.spdup'),'ExitCode':result.returncode,'Output':data,'JournalBytes':records[0].stat().st_size,'VerifiedFileHashes':actual})
fields=['ElapsedMs','CpuMs','AllocatedBytes','PeakWorkingSetBytes']
record={'Status':'PASS (measurement only)','BaselineSource':B,'ModifiedSource':S,'Environment':'Windows26200;24logicalCPUs;32GiB; local synthetic strictAcl=false; no concurrent local benchmark load','Variants':variants,'SeedCommand':'WO20Probe prepare-many <SYNTHETIC_ROOT>','SeedOutput':prepare.stdout,'SeedExitCode':prepare.returncode,'Trials':results,'Medians':{m:{f:statistics.median(x['Output'][f] for x in results if x['Variant']==m) for f in fields} for m in variants},'Limitations':['Nested phase durations overlap.','Whole-process peak includes startup; allocations measure Apply only.','Protected ACL cost and low-end physical devices NOT TESTED.','No general PC speed improvement claim.','Every transaction checkpoint and 779 Flush calls remain.']}
(R/'artifacts/wo22-matched-performance.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
print(json.dumps(record['Medians']))
