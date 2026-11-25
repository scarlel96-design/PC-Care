import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { inspectWorkflow, inspectRunLog, summarizeSdkComparisons, auditDirectory } from '../../tools/chatgpt-qa/sdk-drift-audit.mjs';
import { runCli } from '../../tools/chatgpt-qa/sdk-drift-cli.mjs';

const prefix = 'name: CI\non:\n  pull_request:\njobs:\n  test:\n    runs-on: windows-latest\n    steps:\n      - uses: actions/checkout@v4\n';
const sdk = v => prefix + `      - uses: actions/setup-dotnet@v4\n        with:\n          dotnet-version: '${v}'\n      - name: Build\n        run: dotnet build App.sln\n`;
const pin = '      - name: Select SDK\n        shell: pwsh\n        run: |\n          if(Test-Path global.json){throw "existing"}\n          \'{"sdk":{"version":"10.0.402","rollForward":"disable"}}\' | Set-Content global.json\n          if((dotnet --version) -ne \'10.0.402\'){throw "mismatch"}\n';
const guarded = () => sdk('10.0.402').replace('      - name: Build', pin + '      - name: Build');

const cases = [
  ['floating patch selector', sdk('10.0.x'), 'FLOATING'],
  ['floating minor selector', sdk('10.x'), 'FLOATING'],
  ['floating star selector', sdk('10.0.*'), 'FLOATING'],
  ['exact setup without pin', sdk('10.0.402'), 'EXACT_NO_GUARD'],
  ['exact setup with matching global and assertion', guarded(), 'GUARDED_EXACT'],
  ['guard script without global.json', sdk('10.0.402').replace('      - name: Build', "      - name: Check\n        run: if((dotnet --version) -ne '10.0.402'){throw 'no'}\n      - name: Build"), 'EXACT_NO_GUARD'],
  ['global json without version assertion', sdk('10.0.402').replace('      - name: Build',pin.replace("          if((dotnet --version) -ne '10.0.402'){throw \"mismatch\"}\n",'')+'      - name: Build'), 'EXACT_NO_GUARD'],
  ['setup mismatch with global', sdk('10.0.403').replace('      - name: Build',pin+'      - name: Build'), 'CONFLICT'],
  ['setup version matches but global rollforward not disabled', guarded().replace('"rollForward":"disable"','"rollForward":"latestPatch"'), 'EXACT_NO_GUARD'],
  ['multiple global.json writes', guarded().replace('      - name: Build', pin+'      - name: Build'), 'UNKNOWN'],
  ['no SDK and no build', prefix+'      - run: echo Hello\n', 'NOT_APPLICABLE'],
  ['no SDK but dotnet build', prefix+'      - run: dotnet build App.sln\n', 'UNKNOWN'],
  ['setup without version', prefix+'      - uses: actions/setup-dotnet@v4\n      - run: dotnet build App.sln', 'UNKNOWN'],
  ['comments do not introduce setup', prefix+'      - run: echo hi\n# actions/setup-dotnet@v4\n', 'NOT_APPLICABLE'],
  ['dynamic expression not exact', sdk('${{ matrix.dotnet }}'), 'FLOATING'],
  ['another setup action not dotnet', prefix+'      - uses: actions/setup-node@v4\n        with:\n          node-version: 22\n      - run: echo done\n', 'NOT_APPLICABLE'],
];
for (const [name, content, expected] of cases) test(name, () => {
  assert.equal(inspectWorkflow('workflow.yml',content).classification, expected);
});

test('mixed exact and floating selectors', () => {
  const input = sdk('10.0.402').replace('      - name: Build',"      - uses: actions/setup-dotnet@v4\n        with:\n          dotnet-version: '10.0.x'\n      - name: Build");
  assert.equal(inspectWorkflow('mix.yml',input).classification,'MIXED');
});
test('classification and risk always present', () => {
  const result=inspectWorkflow('a',sdk('10.0.x'));
  assert.equal(result.risk,2); assert.equal(result.performsBuild,true);
});
test('reject wrong parameter types', () => {
  assert.throws(()=>inspectWorkflow(3,''),TypeError);
});
test('extract installed SDK from successful setup-dotnet log',()=>{
  const r=inspectRunLog("dotnet-version: 10.0.402\ndotnet-install: Installed version is 10.0.402\n");
  assert.deepEqual(r.installed,['10.0.402']);assert.deepEqual(r.configured,['10.0.402']);
  assert.equal(r.selectedSdkProven,false);
});
test('extract already installed SDK from failed workflow',()=>{
  const r=inspectRunLog("dotnet-version: 10.0.x\ndotnet-install: .NET Core SDK with version '10.0.404' is already installed.\n##[error] App.cs(11,4): error CsWinRT1032: test");
  assert.deepEqual(r.installed,['10.0.404']);assert.deepEqual(r.compilerErrorCodes,['CsWinRT1032']);
});
test('does not infer SDK from unverified narrative',()=>{
  assert.deepEqual(inspectRunLog('README says SDK 10.0.404').installed,[]);
});
test('deduplicates repeated SDK installations',()=>{
  assert.deepEqual(inspectRunLog('dotnet-install: Installed version is 10.0.402\ndotnet-install: Installed version is 10.0.402').installed,['10.0.402']);
});
test('compare observed SDKs without causal claim',()=>{
  const r=summarizeSdkComparisons([{runId:1,conclusion:'success',installedSdk:'10.0.402'},{runId:2,conclusion:'failure',installedSdk:'10.0.404'}]);
  assert.equal(r.status,'COMPLETE');assert.equal(r.sdkVersionsDiffer,true);assert.equal(r.causeVerified,false);
});
test('incomplete evidence remains partial',()=>{
  const r=summarizeSdkComparisons([{runId:1,conclusion:'failure',installedSdk:null}]);
  assert.equal(r.status,'PARTIAL');assert.equal(r.causeVerified,false);
});
test('duplicate runs remain partial',()=>{
  const r=summarizeSdkComparisons([{runId:1,conclusion:'success',installedSdk:'10.0.402'},{runId:1,conclusion:'failure',installedSdk:'10.0.404'}]);
  assert.equal(r.status,'PARTIAL');
});
test('non-array comparisons rejected',()=>{
  assert.throws(()=>summarizeSdkComparisons([]),TypeError);
});
function withRepo(files, fn) {
  const root=mkdtempSync(join(tmpdir(),'pccare-sdk-audit-'));
  const dir=join(root,'.github','workflows');mkdirSync(dir,{recursive:true});
  for(const [name,text] of Object.entries(files))writeFileSync(join(dir,name),text);
  try {return fn(root);} finally{rmSync(root,{recursive:true,force:true});}
}
test('baseline 26 float and 3 guarded is advisory, not false green',()=>{
  const files={};for(let i=2;i<=27;i++)files[`wo${String(i).padStart(2,'0')}.yml`]=sdk('10.0.x');
  for(let i=28;i<=30;i++)files[`wo${i}.yml`]=guarded();files['qa.yml']=prefix+'      - run: echo ok\n';
  withRepo(files,root=>{const a=auditDirectory(root);assert.equal(a.status,'ADVISORY');assert.equal(a.workflowCount,30);assert.equal(a.counts.FLOATING,26);assert.equal(a.counts.GUARDED_EXACT,3);});
});
test('existing floating baseline stays advisory',()=>{
  withRepo({'wo02.yml':sdk('10.0.x')},root=>{
    const a=auditDirectory(root,{workflows:{'wo02.yml':{classification:'FLOATING'}}});
    assert.equal(a.status,'ADVISORY');assert.equal(a.regressions.length,0);
  });
});
test('loss of pinned guard blocks integration',()=>{
  withRepo({'wo29.yml':sdk('10.0.x')},root=>{
    const a=auditDirectory(root,{workflows:{'wo29.yml':{classification:'GUARDED_EXACT',setupSelectors:[{value:'10.0.402',kind:'EXACT'}]}}});
    assert.equal(a.status,'BLOCKED');assert.equal(a.regressions.length,1);
  });
});
test('silent pinned version change requires qualification',()=>{
  withRepo({'wo29.yml':guarded().replaceAll('10.0.402','10.0.403')},root=>{
    const a=auditDirectory(root,{workflows:{'wo29.yml':{classification:'GUARDED_EXACT',setupSelectors:[{value:'10.0.402',kind:'EXACT'}]}}});
    assert.equal(a.status,'BLOCKED');
  });
});
test('new floating workflow triggers independent review',()=>{
  withRepo({'new.yml':sdk('10.0.x')},root=>{
    const a=auditDirectory(root,{workflows:{}});
    assert.equal(a.status,'BLOCKED');assert.equal(a.regressions[0].reason,'New workflow requires SDK review');
  });
});
test('unexpected removal of workflow blocks',()=>{
  withRepo({'qa.yml':prefix+'      - run: echo pass\n'},root=>{
    const a=auditDirectory(root,{workflows:{'wo29.yml':{classification:'GUARDED_EXACT'}}});
    assert.equal(a.status,'BLOCKED');
  });
});
test('same pinned version stays stable',()=>{
  withRepo({'wo29.yml':guarded()},root=>{
    const a=auditDirectory(root,{workflows:{'wo29.yml':{classification:'GUARDED_EXACT',setupSelectors:[{value:'10.0.402',kind:'EXACT'}]}}});
    assert.equal(a.status,'PASS');assert.deepEqual(a.regressions,[]);
  });
});
test('CLI audit returns structured output',()=>{
  withRepo({'a.yml':sdk('10.0.x')},root=>{
    assert.equal(runCli(['audit','--root',root]).counts.FLOATING,1);
  });
});
test('CLI unknown command throws',()=>{
  assert.throws(()=>runCli(['invalid']));
});
test('pinned setup and global.json without version assertion stays advisory',()=>{
  const onlyPin=guarded().replace("          if((dotnet --version) -ne '10.0.402'){throw \"mismatch\"}\n",'');
  assert.equal(inspectWorkflow('wo29-stage-vault.yml', onlyPin).classification, 'EXACT_NO_GUARD');
});
test('repository configuration fixture matches observed 26/2/1/1 baseline',()=>{
  const baseline=JSON.parse(readFileSync(join(import.meta.dirname,'../../docs/qa/SDK_DRIFT_BASELINE_WO30.json'),'utf8'));
  const files={};
  for (const [name,record] of Object.entries(baseline.workflows)) {
    files[name]=record.classification==='FLOATING' ? sdk('10.0.x') :
      record.classification==='GUARDED_EXACT' ? guarded() :
      record.classification==='EXACT_NO_GUARD' ? guarded().replace("          if((dotnet --version) -ne '10.0.402'){throw \"mismatch\"}\n",'') :
      prefix+'      - run: echo ok\n';
  }
  withRepo(files,root=>{
    const actual=auditDirectory(root,baseline);
    assert.deepEqual(actual.counts,baseline.counts);
    assert.equal(actual.status,'ADVISORY');
    assert.equal(actual.regressions.length,0);
  });
});
