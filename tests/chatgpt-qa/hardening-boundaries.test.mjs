import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync,mkdirSync,writeFileSync,readFileSync,rmSync,readdirSync,copyFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import * as core from '../../tools/chatgpt-qa/core.mjs';
import * as scope from '../../tools/chatgpt-qa/workflow-trigger-audit.mjs';
import * as sdk from '../../tools/chatgpt-qa/sdk-drift-audit.mjs';
import * as env from '../../tools/chatgpt-qa/build-env-provenance.mjs';
import * as ci from '../../tools/chatgpt-qa/ci-observability.mjs';
import * as triage from '../../tools/chatgpt-qa/ci-failure-triage.mjs';
import * as runner from '../../tools/chatgpt-qa/integrated-runner.mjs';
import { inspectXml } from '../../tools/chatgpt-qa/msbuild-xml.mjs';
import { scanCSharp,scanFiles } from '../../tools/chatgpt-qa/cswinrt-static.mjs';
const temporary=fn=>{const d=mkdtempSync(join(tmpdir(),'pc-care-boundary-'));try{return fn(d);}finally{rmSync(d,{recursive:true,force:true});}};
const put=(root,p,text)=>{const path=join(root,p);mkdirSync(join(path,'..'),{recursive:true});writeFileSync(path,text);};
const policy=readFileSync(new URL('../../.github/workflows/chatgpt-qa.yml',import.meta.url),'utf8');
const goodGate=`public static class Av3PhaseGate {\n${core.SECURITY_FLAGS.map(n=>`public static readonly bool ${n} = false;`).join('\n')}\npublic const Av3ProductionStatus CurrentStatus = Av3ProductionStatus.ReadOnlyValidated;\n}`;
const legacy=(paths=scope.QA_ONLY_PATHS)=>`name: Legacy\non:\n  pull_request:\n    paths-ignore:\n${paths.map(p=>`      - '${p}'`).join('\n')}\njobs:\n  build:\n    runs-on: windows-latest\n`;
const guarded=`name: Build\non:\n  workflow_dispatch:\njobs:\n  build:\n    runs-on: windows-latest\n    steps:\n      - uses: actions/setup-dotnet@v4\n        with:\n          dotnet-version: '10.0.402'\n      - name: Pin\n        shell: pwsh\n        run: |\n          if(Test-Path global.json){throw 'existing'}\n          '{"sdk":{"version":"10.0.402","rollForward":"disable"}}' | Set-Content global.json\n          if((dotnet --version) -ne '10.0.402'){throw 'mismatch'}\n      - name: Build\n        run: dotnet build App.sln\n`;
const run={id:1,status:'completed',conclusion:'success',workflow_id:3,head_sha:'a'.repeat(40),event:'pull_request',run_attempt:1};
const job={id:2,status:'completed',conclusion:'success',started_at:'2026-11-25T01:00:00Z',completed_at:'2026-11-25T01:01:00Z'};
const snapshot=()=>({total_count:1,workflow_runs:[{...run}],jobs_by_run:{1:{total_count:1,jobs:[{...job}]}}});
const failure=(id=1,jobId=2)=>({runId:id,jobId,conclusion:'failure',logSummary:{emittedErrorLines:2,uniqueLocations:1,codes:{CsWinRT1032:2}}});
const approvals={codexRefKnown:true,testExecutionObserved:true,githubActionsObserved:true};
const packageXml='<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Version="2.2.0" Include="Microsoft.WindowsAppSDK" /></ItemGroup></Project>';

test('R01 valid source with comments stays accepted',()=>assert.equal(core.checkSecuritySource('// sample\n'+goodGate).status,'PASS'));
test('R01 conditional alternative cannot establish active source',()=>assert.notEqual(core.checkSecuritySource('#if DEBUG\n'+goodGate+'\n#else\n'+goodGate+'\n#endif').status,'PASS'));
test('R01 string literal must not supply flags',()=>assert.notEqual(core.checkSecuritySource('var x = """\n'+goodGate+'\n""";').status,'PASS'));
test('R02 exact historical inventory passes model only',()=>temporary(root=>{for(const name of scope.HISTORICAL_WORKFLOWS)put(root,'.github/workflows/'+name,legacy());const a=scope.auditWorkflowDirectory(root);assert.equal(a.status,'PASS');assert.equal(a.requiredChecksVerified,false);}));
test('R02 duplicate exclusion blocks',()=>assert.equal(scope.analyzeWorkflow('wo02-windows-security.yml',legacy([...scope.QA_ONLY_PATHS,scope.QA_ONLY_PATHS[0]])).status,'FAIL'));
test('R02 unexpected branch/type filters block',()=>assert.equal(scope.analyzeWorkflow('wo02-windows-security.yml',legacy().replace('    paths-ignore:','    branches: [main]\n    paths-ignore:')).status,'FAIL'));
test('R02 changed path traversal and wider patterns are rejected',()=>{assert.throws(()=>scope.classifyChangedFiles(['docs/qa/../rework/a']));assert.throws(()=>scope.classifyChangedFiles(['src/a'],['src/**']));});
test('R03 supported guard remains recognized',()=>assert.equal(sdk.inspectWorkflow('x',guarded).classification,'GUARDED_EXACT'));
for(const [name,text] of [
 ['conditional pin',guarded.replace('        shell: pwsh','        if: false\n        shell: pwsh')],
 ['conditional setup',guarded.replace('        with:','        if: false\n        with:')],
 ['wrong shell',guarded.replace('shell: pwsh','shell: bash')],
 ['quoted command instead of executing',guarded.replace("          '{\"sdk\"", "          Write-Output '{\"sdk\"")],
 ['guard compares wrong version',guarded.replace("-ne '10.0.402'","-ne '10.0.404'")],
 ['build working-directory different',guarded.replace('        run: dotnet build','        working-directory: sub\n        run: dotnet build')],
 ['later pin removal',guarded+'      - run: Remove-Item global.json\n'],
 ['multiple jobs',guarded+'  another:\n    runs-on: windows-latest\n    steps:\n      - run: dotnet build Other.sln\n'],
])test('R03 does not certify '+name,()=>assert.notEqual(sdk.inspectWorkflow('x',text).classification,'GUARDED_EXACT'));
test('R03 one-sided SDK comparison is partial',()=>assert.equal(sdk.summarizeSdkComparisons([{runId:1,conclusion:'success',installedSdk:'10.0.402'}]).status,'PARTIAL'));
test('R04 nine prerequisite checks without QA10 self-attestation remain on HOLD',()=>{const q=core.premergeChecks(core.QA_TASKS.slice(0,9).map(id=>({id,status:'PASS'})),approvals);assert.equal(q.details.structurallyComplete,true);assert.equal(q.details.ready,false);assert.equal(q.status,'BLOCKED');});
test('R05 reject Windows aliases, reserved names, streams and case duplicates',()=>{for(const p of ['src/CON.cs','src/a.cs.','src/a.cs ','src/a.cs:secret','src/a?.cs'])assert.throws(()=>core.safeRelativePath(p));assert.equal(core.checkImpact([],['docs/qa/a','DOCS/QA/A']).status,'FAIL');});
test('R06 reject malformed diagnostic objects without poisoning prototype',()=>{for(const category of ['__proto__','constructor','prototype',null,{},'toString']){const q=core.validateDiagnosticFixtures([{id:'x',category,expected:'poison',observation:'invalid case',rationale:'invalid case',mayMutate:false}]);assert.equal(q.status,'FAIL');assert.equal(Object.prototype.poison,undefined);}});
test('R07 extra broad PR path fails',()=>assert.equal(core.checkCiScope(policy.replace("      - 'docs/qa/**'","      - 'docs/qa/**'\n      - 'src/**'")).status,'FAIL'));
test('R07 pull_request_target or inline push fails',()=>{for(const trigger of ['  pull_request_target:\n','  push: [main]\n'])assert.equal(core.checkCiScope(policy.replace('  workflow_dispatch:',trigger+'  workflow_dispatch:')).status,'FAIL');});
test('R07 overridden write permissions fail',()=>assert.equal(core.checkCiScope(policy.replace('    runs-on:', '    permissions:\n      contents: write\n    runs-on:')).status,'FAIL'));
test('R08 XML parser reads reversed single-quoted attributes',()=>assert.equal(core.auditDependencies("<Project><ItemGroup><PackageVersion Version='2.*' Include='A'/></ItemGroup></Project>").status,'FAIL'));
test('R08 XML parser fails closed on malformed tags, DTD and duplicate attributes',()=>{for(const text of ['<Project><ItemGroup></Project>','<!DOCTYPE Project><Project/>','<Project x="a" x="b"/>','<Project><!-- no end'])assert.throws(()=>inspectXml(text));});
test('R08 XML conditional package is unresolved',()=>assert.equal(core.auditDependencies('<Project><ItemGroup Condition="false"><PackageVersion Include="X" Version="1.2.3"/></ItemGroup></Project>').status,'FAIL'));
test('R09 mismatched error counters do not qualify common codes',()=>{const x=failure();x.logSummary.emittedErrorLines=99;const a=triage.summarizeFailureEvidence({expectedFailures:1,runs:[x]});assert.equal(a.status,'PARTIAL');assert.deepEqual(a.commonToAllFailures,[]);});
test('R09 duplicated jobs across different runs cannot qualify',()=>assert.equal(triage.summarizeFailureEvidence({expectedFailures:2,runs:[failure(),failure(3,2)]}).status,'PARTIAL'));
test('R09 missing expected count does not certify historical coverage',()=>assert.equal(triage.summarizeFailureEvidence({runs:[failure()]}).status,'PARTIAL'));
test('R10 job attached to different run is excluded',()=>{const x=snapshot();x.jobs_by_run[1].jobs[0].run_id=99;const a=ci.analyzeSnapshot(x);assert.equal(a.status,'PARTIAL');assert.equal(a.jobTiming.completedJobWallSecondsObserved,null);});
test('R10 run totals smaller than actual cannot qualify',()=>{const x=snapshot();x.total_count=0;assert.equal(ci.analyzeSnapshot(x).status,'PARTIAL');});
test('R10 unobserved rerun attempts prevent complete timing',()=>{const x=snapshot();x.workflow_runs[0].run_attempt=3;assert.equal(ci.analyzeSnapshot(x).status,'PARTIAL');});
test('R10 untrusted step names cannot modify object prototypes',()=>{const x=snapshot();x.jobs_by_run[1].jobs[0].steps=[{name:'__proto__',conclusion:'failure'}];assert.equal(ci.analyzeSnapshot(x).failedStepCounts['__proto__'],1);assert.equal(Object.prototype.polluted,undefined);});
test('R11 nested try/catch collection return remains review not clear',()=>assert.equal(scanCSharp('IEnumerable<int> F(){try {return [1];} catch {return [2];}}').status,'ADVISORY'));
test('R12 invalid and advisory coverage cannot become PASS',()=>{for(const x of [{highConfidence:0,advisory:0,scannedFiles:-1,status:'CLEAR'},{highConfidence:0,advisory:1,scannedFiles:1,status:'CLEAR'},scanFiles({})])assert.notEqual(runner.classifyOutcome('cswinrt-source-guard',0,x).status,'PASS');});
test('R12 premerge payload cannot forge approval',()=>assert.equal(runner.classifyOutcome('premerge-readiness',0,{status:'PASS',details:{ready:true}}).status,'BLOCKED'));
test('R13 package version must be literal and unconditional',()=>{assert.equal(env.parseCentralPackages(packageXml).windowsAppSdkDeclared,'2.2.0');assert.throws(()=>env.parseCentralPackages(packageXml.replace('2.2.0','2.*')));assert.throws(()=>env.parseCentralPackages(packageXml.replace('<ItemGroup>','<ItemGroup Condition="false">')));});
test('R13 project conditional or commented target does not prove framework',()=>assert.equal(env.parseAppProject('<Project><PropertyGroup Condition="false"><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>').targetFramework,null));
test('R14 empty suite blocks without launching other product tests',()=>temporary(root=>{const result=runner.runSuite(root,runner.parseArgs(['--only','unit-tests']),{executor:()=>{throw Error('must not run')}});assert.equal(result.checks[0].status,'BLOCKED');assert.equal(result.checks[0].details.total,0);}));
test('R14 existing evidence is never overwritten',()=>temporary(root=>{put(root,'docs/qa/existing.json','original');assert.throws(()=>runner.relativeFile(root,'docs/qa/existing.json',{mustExist:false,allowOutput:true}));assert.equal(readFileSync(join(root,'docs/qa/existing.json'),'utf8'),'original');}));
test('R14 empty aggregate cannot certify success',()=>assert.equal(runner.summarize([]).status,'BLOCKED'));

test('R07 command text outside a run step cannot prove execution',()=>assert.equal(core.checkCiScope(policy.replace('run: node tools/chatgpt-qa/integrated-runner.mjs','name: node tools/chatgpt-qa/integrated-runner.mjs')).status,'FAIL'));
test('R07 conditional or duplicate root configuration is blocked',()=>{assert.equal(core.checkCiScope(policy.replace('        run: node tools/chatgpt-qa/integrated-runner.mjs','        if: false\n        run: node tools/chatgpt-qa/integrated-runner.mjs')).status,'FAIL');assert.equal(core.checkCiScope(policy+'\npermissions: write-all\n').status,'FAIL');});
test('R14 required inventory hash mismatch cannot qualify even if count matches',()=>temporary(root=>{
  const actual=new URL('../../',import.meta.url);const dir=new URL('tests/chatgpt-qa/',actual);mkdirSync(join(root,'tests/chatgpt-qa'),{recursive:true});
  for(const n of readdirSync(dir).filter(n=>n.endsWith('.test.mjs')))copyFileSync(new URL(n,dir),join(root,'tests/chatgpt-qa',n));
  put(root,'docs/qa/QA_TEST_INVENTORY_WO30.json',readFileSync(new URL('docs/qa/QA_TEST_INVENTORY_WO30.json',actual),'utf8'));
  put(root,'tests/chatgpt-qa/core.test.mjs',"// changed test inventory\n");
  const result=runner.runSuite(root,runner.parseArgs(['--only','unit-tests']),{executor:()=>({exitCode:0,stdout:'# tests 400\n# pass 400\n# fail 0\n# skipped 0',stderr:''})});
  assert.equal(result.checks[0].status,'BLOCKED');assert.match(result.checks[0].reason,/hash mismatch/);
}));

test('R01 flags in a different class cannot certify AV3 gate',()=>assert.equal(core.checkSecuritySource(goodGate.replace('class Av3PhaseGate','class Other')).status,'FAIL'));
test('R01 later static-constructor reassignment blocks literal declaration',()=>assert.equal(core.checkSecuritySource(goodGate.slice(0,-1)+'static Av3PhaseGate(){ ProductionWriterEnabled = true; }\n}').status,'FAIL'));
