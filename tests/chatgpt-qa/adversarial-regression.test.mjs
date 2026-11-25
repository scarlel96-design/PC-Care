/** Negative acceptance tests for the reviewed WO-30 QA package.
 * Run with QA_ROOT pointing to the integration checkout.
 * Set PR3_ROOT to the separate CI-scope checkout to include its two tests.
 * A failing assertion records an unsatisfied acceptance criterion, not a product runtime failure.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const load=(name,base=root)=>import(pathToFileURL(join(base,'tools/chatgpt-qa',name)));
const core=await load('core.mjs');
const runner=await load('integrated-runner.mjs');
const sdk=await load('sdk-drift-audit.mjs');
const triage=await load('ci-failure-triage.mjs');
const ci=await load('ci-observability.mjs');
const cs=await load('cswinrt-static.mjs');
const env=await load('build-env-provenance.mjs');
const pr3=await load('workflow-trigger-audit.mjs');
const gate=core.SECURITY_FLAGS.map(n=>`public static readonly bool ${n} = false;`).join('\n')+'\npublic const Av3ProductionStatus CurrentStatus = Av3ProductionStatus.ReadOnlyValidated;';
const workflow=readFileSync(join(root,'.github/workflows/chatgpt-qa.yml'),'utf8');
const lateSdk=`name: Demo\non:\n  workflow_dispatch:\njobs:\n  build:\n    runs-on: windows-latest\n    steps:\n      - uses: actions/setup-dotnet@v4\n        with:\n          dotnet-version: '10.0.402'\n      - name: Build\n        run: dotnet build App.sln\n      - name: Pin too late\n        run: |\n          '{"sdk":{"version":"10.0.402","rollForward":"disable"}}' | Set-Content global.json\n          if((dotnet --version) -ne '10.0.402'){throw 'mismatch'}\n`;
const temp=fn=>{const d=mkdtempSync(join(tmpdir(),'pccare-negative-'));try{return fn(d);}finally{rmSync(d,{recursive:true,force:true});}};
const put=(d,p,s)=>{const f=join(d,p);mkdirSync(resolve(f,'..'),{recursive:true});writeFileSync(f,s);};
const job={id:2,status:'completed',started_at:'2026-11-25T01:00:00Z',completed_at:'2026-11-25T01:01:00Z'};
const run={id:1,status:'completed',conclusion:'success',workflow_id:3};
const legacy=ignores=>'on:\n  pull_request:\n    paths-ignore:\n'+ignores.map(x=>`      - '${x}'`).join('\n')+'\njobs:\n  build:\n    runs-on: windows-latest\n';
test('R01a AV3: commented false declarations must not mask active true flag',()=>assert.notEqual(core.checkSecuritySource('/*\n'+gate+'\n*/\npublic static class Av3PhaseGate { public static readonly bool ProductionWriterEnabled = true; }').status,'PASS'));
test('R01b AV3: inactive conditional declarations must not count as active policy',()=>assert.notEqual(core.checkSecuritySource('#if NEVER_DEFINED\n'+gate+'\n#endif').status,'PASS'));
test('R02a PR3: extra product path exclusion must be rejected',{skip:!pr3},()=>assert.notEqual(pr3.analyzeWorkflow('wo27-windows-validation.yml',legacy([...pr3.QA_ONLY_PATHS,'src/**'])).status,'PASS'));
test('R02b PR3: 26 copies of WO02 do not establish WO02 through WO27',{skip:!pr3},()=>temp(d=>{for(let i=0;i<26;i++)put(d,`.github/workflows/wo02-copy-${i}.yml`,legacy(pr3.QA_ONLY_PATHS));assert.notEqual(pr3.auditWorkflowDirectory(d).status,'PASS');}));
test('R03a SDK: pin and assertion after build must not qualify a guarded build',()=>assert.notEqual(sdk.inspectWorkflow('late.yml',lateSdk).classification,'GUARDED_EXACT'));
test('R03b SDK: comments do not execute a pin or assertion',()=>assert.notEqual(sdk.inspectWorkflow('comment.yml',lateSdk.replace("          '{\"sdk\"","          # '{\"sdk\"").replace('          if((dotnet','          # if((dotnet')).classification,'GUARDED_EXACT'));
test('R04 premerge: caller booleans and self-reported PASS must not set approval-ready',()=>assert.notEqual(core.premergeChecks(core.QA_TASKS.map(id=>({id,status:'PASS'})),{codexRefKnown:true,testExecutionObserved:true,githubActionsObserved:true}).details.ready,true));
test('R05a impact: integration-owned manual workflow must be consistently recognized',()=>assert.equal(core.checkImpact([],['.github/workflows/chatgpt-qa-integration.yml']).status,'PASS'));
test('R05b impact: duplicate paths must not silently qualify a complete list',()=>assert.notEqual(core.checkImpact(['src/a.cs','src/a.cs'],['docs/qa/x.md','docs/qa/x.md']).status,'PASS'));
test('R06 diagnostic fixture: invalid categories must not modify Object.prototype',()=>{const key='wo30AuditProbe';try{core.validateDiagnosticFixtures([{id:'bad',category:'__proto__',expected:key,observation:'test evidence',rationale:'test evidence',mayMutate:false}]);assert.equal(Object.prototype[key],undefined);}finally{delete Object.prototype[key];}});
test('R07 core CI scope: commented policies must not satisfy policy requirements',()=>assert.notEqual(core.checkCiScope('concurrency:\n  cancel-in-progress: true\n'+workflow.split('\n').map(l=>'# '+l).join('\n')).status,'PASS'));
test('R08 dependency audit: reversed attributes must still detect floating version',()=>assert.equal(core.auditDependencies('<Project><PackageVersion Include="A" Version="1.0.0"/><PackageVersion Version="2.*" Include="B" /></Project>').findings.some(x=>x.code==='NON_PINNED_VERSION'),true));
test('R09 failure triage: zero occurrences cannot be a common observed error',()=>assert.equal(triage.summarizeFailureEvidence({expectedFailures:1,runs:[{runId:1,jobId:2,conclusion:'failure',logSummary:{emittedErrorLines:0,uniqueLocations:0,codes:{CsWinRT1032:0}}}]}).commonToAllFailures.includes('CsWinRT1032'),false));
test('R10a CI timing: duplicate job must not yield a complete doubled duration',()=>{const a=ci.analyzeSnapshot({total_count:1,workflow_runs:[run],jobs_by_run:{1:{jobs:[job,job],total_count:1}}});assert.ok(a.status!=='COMPLETE'||a.jobTiming.completedJobWallSecondsObserved===60);});
test('R10b CI timing: missing total count cannot certify empty coverage',()=>assert.notEqual(ci.analyzeSnapshot({workflow_runs:[]}).status,'COMPLETE'));
test('R11 CsWinRT: return in nested if block must be a review candidate',()=>assert.notEqual(cs.scanCSharp('IReadOnlyList<int> F(bool b) { if (b) { return [1]; } return Array.Empty<int>(); }').status,'CLEAR'));
test('R12a integration: advisory-only CsWinRT findings must not disappear as PASS',()=>assert.notEqual(runner.classifyOutcome('cswinrt-source-guard',0,cs.scanFiles({'x.cs':'void F(){ G(items: [1]); }'})).status,'PASS'));
test('R12b integration: empty CsWinRT scan must not pass',()=>assert.notEqual(runner.classifyOutcome('cswinrt-source-guard',0,cs.scanFiles({})).status,'PASS'));
test('R13a build environment: commented XML declaration must not establish a package version',()=>assert.equal(env.parseCentralPackages('<Project><!-- <PackageVersion Include="Microsoft.WindowsAppSDK" Version="2.2.0"/> --></Project>').windowsAppSdkDeclared,null));
test('R13b build environment: empty project and workflow folder cannot establish MATCH',()=>temp(d=>{mkdirSync(join(d,'.github/workflows'),{recursive:true});put(d,'Directory.Packages.props','<Project/>');put(d,'src/SmartPerformanceDoctor.App/SmartPerformanceDoctor.App.csproj','<Project/>');assert.notEqual(env.auditCheckout(d).status,'MATCH');}));
test('R14 test inventory: one passing test must not qualify the entire required test inventory',()=>temp(d=>{put(d,'tests/chatgpt-qa/only.test.mjs',"import test from 'node:test'; test('only',()=>{});\n");const o=runner.parseArgs(['--only','unit-tests']);const prev=process.env.NODE_TEST_CONTEXT;delete process.env.NODE_TEST_CONTEXT;try{const result=runner.runSuite(d,o);const check=result.checks.find(x=>x.id==='unit-tests');assert.equal(check.details.total,1,'Ensure the child emits a valid one-test TAP summary');assert.notEqual(check.status,'PASS');}finally{if(prev!==undefined)process.env.NODE_TEST_CONTEXT=prev;}}));
