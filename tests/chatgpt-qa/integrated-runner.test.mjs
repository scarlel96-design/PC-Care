import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, symlinkSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  SCHEMA, CHECK_IDS, relativeFile, parseArgs, makePlan, classifyOutcome,
  executeNode, summarize, runSuite, main,
} from '../../tools/chatgpt-qa/integrated-runner.mjs';

const opts=(over={})=>({ profile:'offline', root:'.', strict:false, list:false, only:null,
  'codex-files':null,'qa-files':null,'ui-paths':null,'premerge-checks':null,'source-sha':null,'output':null,...over });
const fixture=()=>{
  const root=mkdtempSync(join(tmpdir(),'pc-care-integrated-'));
  for(const folder of ['tools/chatgpt-qa','tests/chatgpt-qa/fixtures','docs/qa'])mkdirSync(join(root,folder),{recursive:true});
  const files={
    'tools/chatgpt-qa/cli.mjs':'', 'tools/chatgpt-qa/sdk-drift-cli.mjs':'',
    'tools/chatgpt-qa/build-env-cli.mjs':'', 'tools/chatgpt-qa/ci-failure-triage-cli.mjs':'',
    'tools/chatgpt-qa/ci-observability-cli.mjs':'','tools/chatgpt-qa/cswinrt-static-cli.mjs':'',
    'docs/qa/CI_FAILURE_EVIDENCE_WO30.json':'{}',
    'docs/qa/SDK_DRIFT_RUN_EVIDENCE_WO30.json':'{}',
    'docs/qa/SDK_DRIFT_BASELINE_WO30.json':'{}',
    'docs/qa/BUILD_ENV_COMPARE_WO23_WO30.json':'{}',
    'tests/chatgpt-qa/fixtures/ci-synthetic-snapshot.json':'{}',
    'tests/chatgpt-qa/sample.test.mjs':'',
  };
  for(const [key,value] of Object.entries(files))writeFileSync(join(root,key),value);
  return root;
};
const withRoot=(fn)=>{const root=fixture();try{return fn(root);}finally{rmSync(root,{recursive:true,force:true});}};
const runner=(root,item)=> {
  if(item.kind==='tests')return {exitCode:0,stdout:'# tests 7\n# pass 7\n# fail 0\n# skipped 0\n',stderr:''};
  if(item.id==='build-env-run-compare')return {exitCode:1,stdout:JSON.stringify({status:'REVIEW',differences:[{key:'installedSdk'}],causeVerified:false}),stderr:''};
  if(item.id==='ci-failure-evidence')return {exitCode:0,stdout:JSON.stringify({status:'COMPLETE',failedRuns:26,commonToAllFailures:['CsWinRT1032']}),stderr:''};
  if(item.id==='sdk-run-compare')return {exitCode:0,stdout:JSON.stringify({status:'COMPLETE',sdkVersionsDiffer:true,causeVerified:false}),stderr:''};
  return {exitCode:0,stdout:JSON.stringify({status:'COMPLETE'}),stderr:''};
};

test('schema and declared check IDs remain unique',()=>{
  assert.match(SCHEMA,/integration/);
  assert.equal(CHECK_IDS.length,new Set(CHECK_IDS).size);
  assert.equal(CHECK_IDS.length,13);
});
test('accepts offline and checkout profiles',()=>{
  assert.equal(parseArgs(['--profile','checkout']).profile,'checkout');
  assert.equal(parseArgs(['--profile','offline']).profile,'offline');
});
test('default profile is explicit offline',()=>assert.equal(parseArgs([]).profile,'offline'));
test('strict flag is parsed',()=>assert.equal(parseArgs(['--strict']).strict,true));
test('list flag is parsed',()=>assert.equal(parseArgs(['--list']).list,true));
test('unknown option fails',()=>assert.throws(()=>parseArgs(['--shell','pwsh'])));
test('duplicate option fails',()=>assert.throws(()=>parseArgs(['--root','.','--root','.'])));
test('duplicate strict fails',()=>assert.throws(()=>parseArgs(['--strict','--strict'])));
test('option without value fails',()=>assert.throws(()=>parseArgs(['--profile'])));
test('unknown profile fails',()=>assert.throws(()=>parseArgs(['--profile','unsafe'])));
test('partial file ownership pair fails',()=>assert.throws(()=>parseArgs(['--codex-files','test.txt'])));
test('duplicate check selection fails',()=>assert.throws(()=>parseArgs(['--only','unit-tests,unit-tests'])));
test('unknown check selection fails',()=>assert.throws(()=>parseArgs(['--only','rm-rf'])));
test('valid check selection succeeds',()=>assert.deepEqual(parseArgs(['--only','unit-tests,sdk-run-compare']).only,['unit-tests','sdk-run-compare']));
test('SHA validation requires full commit',()=>assert.throws(()=>parseArgs(['--source-sha','abcdef'])));
test('SHA accepts 40 hexadecimal digits',()=>assert.equal(parseArgs(['--source-sha','a'.repeat(40)])['source-sha'],'a'.repeat(40)));
test('relative file rejects traversal',()=>withRoot(root=>assert.throws(()=>relativeFile(root,'../escape.txt'))));
test('relative file rejects absolute paths',()=>withRoot(root=>assert.throws(()=>relativeFile(root,'/etc/passwd'))));
test('relative file rejects empty paths',()=>withRoot(root=>assert.throws(()=>relativeFile(root,''))));
test('relative file rejects doubled separators',()=>withRoot(root=>assert.throws(()=>relativeFile(root,'docs//qa/x'))));
test('relative file rejects non-existing input',()=>withRoot(root=>assert.throws(()=>relativeFile(root,'docs/qa/absent.json'))));
test('relative file accepts present input',()=>withRoot(root=>assert.ok(relativeFile(root,'docs/qa/CI_FAILURE_EVIDENCE_WO30.json'))));
test('output must stay in docs/qa',()=>withRoot(root=>assert.throws(()=>relativeFile(root,'src/new.cs',{mustExist:false,allowOutput:true}))));
test('output allows docs/qa',()=>withRoot(root=>assert.match(relativeFile(root,'docs/qa/result.json',{mustExist:false,allowOutput:true}),/result\.json$/)));
test('missing tool payload is ERROR',()=>assert.equal(classifyOutcome('sdk-run-compare',0,null).status,'ERROR'));
test('unexpected exit 2 is ERROR',()=>assert.equal(classifyOutcome('sdk-run-compare',2,{status:'PASS'}).status,'ERROR'));
test('unknown status is ERROR',()=>assert.equal(classifyOutcome('sdk-run-compare',0,{status:'APPROVED'}).status,'ERROR'));
test('status PASS with exit 1 is ERROR',()=>assert.equal(classifyOutcome('sdk-run-compare',1,{status:'PASS'}).status,'ERROR'));
test('known SDK comparison maps COMPLETE to PASS',()=>assert.equal(classifyOutcome('sdk-run-compare',0,{status:'COMPLETE'}).status,'PASS'));
test('known SDK drift maps ADVISORY to WARN',()=>assert.equal(classifyOutcome('sdk-workflow-drift',0,{status:'ADVISORY'}).status,'WARN'));
test('known build environment difference is WARN',()=>assert.equal(classifyOutcome('build-env-run-compare',1,{status:'REVIEW'}).status,'WARN'));
test('build environment regression is BLOCKED',()=>assert.equal(classifyOutcome('build-env-checkout',1,{status:'REVIEW'}).status,'BLOCKED'));
test('premerge BLOCKED remains BLOCKED',()=>assert.equal(classifyOutcome('premerge-readiness',1,{status:'BLOCKED'}).status,'BLOCKED'));
test('source risk count is required',()=>assert.equal(classifyOutcome('cswinrt-source-guard',0,{status:'PASS'}).status,'ERROR'));
test('known source risk does not assert compiler failure',()=>assert.equal(classifyOutcome('cswinrt-source-guard',0,{highConfidence:10}).status,'WARN'));
test('clean source risk scan is PASS',()=>assert.equal(classifyOutcome('cswinrt-source-guard',0,{highConfidence:0,advisory:0,scannedFiles:1,status:'CLEAR'}).status,'PASS'));
test('suite summary separates warnings and skips',()=>assert.deepEqual(summarize([{status:'PASS'},{status:'WARN'},{status:'SKIPPED'}]).counts,{PASS:1,WARN:1,BLOCKED:0,SKIPPED:1,ERROR:0}));
test('suite is PARTIAL when inputs missing',()=>assert.equal(summarize([{status:'PASS'},{status:'SKIPPED'}]).status,'PARTIAL'));
test('suite is BLOCKED on failed gate',()=>assert.equal(summarize([{status:'BLOCKED'},{status:'PASS'}]).status,'BLOCKED'));
test('suite is BLOCKED on tool error',()=>assert.equal(summarize([{status:'ERROR'},{status:'PASS'}]).status,'BLOCKED'));
test('suite is REVIEW when only warning remains',()=>assert.equal(summarize([{status:'WARN'},{status:'PASS'}]).status,'REVIEW'));
test('suite is PASS only with complete clean checks',()=>assert.equal(summarize([{status:'PASS'},{status:'PASS'}]).status,'PASS'));
test('offline plan skips product source operations',()=>withRoot(root=>{
  const p=makePlan(root,opts());
  assert.equal(p.find(x=>x.id==='cswinrt-source-guard').eligible,false);
  assert.equal(p.find(x=>x.id==='wo29-source-evidence').eligible,false);
}));
test('offline run with incomplete required test inventory blocks, not product GO',()=>withRoot(root=>{
  const r=runSuite(root,opts(),{executor:runner});
  assert.equal(r.schema,SCHEMA);
  assert.equal(r.status,'BLOCKED');
  assert.equal(r.checks.find(x=>x.id==='unit-tests').status,'BLOCKED');
  assert.equal(r.releaseDecision,'NOT_ASSESSED');
  assert.equal(r.productPhase1Go,false);
  assert.equal(r.summary.counts.ERROR,0);
  assert.equal(r.summary.counts.WARN,1);
  assert.equal(r.summary.counts.SKIPPED,8);
}));
test('offline runner marks synthetic parser as synthetic evidence',()=>withRoot(root=>{
  const r=runSuite(root,opts(),{executor:runner});
  const c=r.checks.find(x=>x.id==='ci-observability-synthetic');
  assert.equal(c.status,'PASS');assert.equal(c.evidence,'SYNTHETIC_FIXTURE');
}));
test('only selected checks execute; others record SKIPPED',()=>withRoot(root=>{
  const called=[];
  const r=runSuite(root,opts({only:['sdk-run-compare']}),{executor:(a,b)=>{called.push(b.id);return runner(a,b)}});
  assert.deepEqual(called,['sdk-run-compare']);
  assert.equal(r.summary.executed,1);
  assert.equal(r.checks.length,13);
}));
test('incomplete output is ERROR, never PASS',()=>withRoot(root=>{
  const r=runSuite(root,opts({only:['sdk-run-compare']}),{executor:()=>({exitCode:0,stdout:'not json',stderr:''})});
  assert.equal(r.checks.find(x=>x.id==='sdk-run-compare').status,'ERROR');
  assert.equal(r.status,'BLOCKED');
}));
test('synthetic corrupted unit summary is ERROR',()=>withRoot(root=>{
  const r=runSuite(root,opts({only:['unit-tests']}),{executor:()=>({exitCode:0,stdout:'# tests 5\n# pass 4\n# fail 0\n# skipped 0',stderr:''})});
  assert.equal(r.checks.find(x=>x.id==='unit-tests').status,'ERROR');
}));
test('failed unit tests block integration',()=>withRoot(root=>{
  const r=runSuite(root,opts({only:['unit-tests']}),{executor:()=>({exitCode:1,stdout:'# tests 5\n# pass 4\n# fail 1\n# skipped 0',stderr:''})});
  assert.equal(r.checks.find(x=>x.id==='unit-tests').status,'BLOCKED');
}));
test('user-supplied changed path lists enable impact analysis',()=>withRoot(root=>{
  writeFileSync(join(root,'docs/qa/codex-files.txt'),'src/Engine.cs\n');
  writeFileSync(join(root,'docs/qa/qa-files.txt'),'tools/chatgpt-qa/cli.mjs\n');
  const p=makePlan(root,opts({'codex-files':'docs/qa/codex-files.txt','qa-files':'docs/qa/qa-files.txt'}));
  assert.equal(p.find(x=>x.id==='change-impact').eligible,true);
}));
test('unavailable independent workflow branch is recorded as skipped',()=>withRoot(root=>{
  const p=makePlan(root,opts({profile:'checkout'}));
  assert.equal(p.find(x=>x.id==='workflow-trigger-scope').eligible,false);
}));
test('main --list lists all contracts without executing',()=>withRoot(root=>{
  const r=main(['--root',root,'--list'],{executor:()=>{throw Error('must not execute')}});
  assert.equal(r.checks.length,13);
}));
test('main permits explicit report output in docs/qa',()=>withRoot(root=>{
  const r=main(['--root',root,'--only','ci-failure-evidence','--output','docs/qa/local-report.json'],{executor:runner});
  const saved=JSON.parse(readFileSync(join(root,'docs/qa/local-report.json'),'utf8'));
  assert.equal(saved.schema,SCHEMA);
  assert.equal(r.summary.executed,1);
}));
test('runSuite validates operator-provided paths before running',()=>withRoot(root=>{
  assert.throws(()=>runSuite(root,opts({'ui-paths':'../outside.txt'}),{executor:runner}));
}));
test('source SHA is retained but not invented',()=>withRoot(root=>{
  const r=runSuite(root,opts({'source-sha':'a'.repeat(40),only:['sdk-run-compare']}),{executor:runner});
  assert.equal(r.sourceSha,'a'.repeat(40));
  assert.equal(r.remoteCiObservedByThisRun,false);
}));
