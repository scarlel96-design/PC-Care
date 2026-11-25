import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  QA_TASKS, SECURITY_FLAGS, safeRelativePath, checkImpact, checkSecuritySource,
  checkEvidence, median, analyzePerformance, scanXaml, validateDiagnosticFixtures,
  auditDependencies, checkCiScope, premergeChecks,
} from '../../tools/chatgpt-qa/core.mjs';

const local = p => readFileSync(fileURLToPath(new URL(p, import.meta.url)), 'utf8');
const makeFlags=()=>`public static class Av3PhaseGate {\npublic const Av3ProductionStatus CurrentStatus = Av3ProductionStatus.ReadOnlyValidated;\n${SECURITY_FLAGS.map(f=>`public static readonly bool ${f} = false;`).join('\n')}\n}`;
const regression=(passed,skipped) => `Source: ${'e'.repeat(40)}\nOutput: Passed! - Failed: 0, Passed: ${passed}, Skipped: ${skipped}, Total: ${passed+skipped}\nExit: 0`;
const evidence = () => ({
  regression: regression(1445,111), securityLab:regression(36,0),
  gates:{Source:'e'.repeat(40),Output:Object.fromEntries(SECURITY_FLAGS.map(f=>[f,'false']))},
  readiness:'Phase 1 NO-GO. The file remains blocked.',
  execution:'WO-29 PARTIAL. Windows tests remain open. App: 1,445 passed; SecurityLab: 36 passed. 26 readers. 22 staging interruptions. 33 v5 opens and 132 authenticated file comparisons.',
});
const metrics = (base=100,mode='baseline',i=0) => ({
  Transactions:4096,Kind:mode,ExitCode:0,Sequence:i,
  Output:{AllocatedBytes:base,ElapsedMs:base/10,CpuMs:base/8,PeakSampledHandles:20,
    Phases:[{Name:'NativeIdentityCheck',Calls:55}]},
});

test('inventory declares exactly ten tasks',()=>{assert.equal(QA_TASKS.length,10);assert.equal(new Set(QA_TASKS).size,10);});
// QA01: overlap prevention and path ownership.
test('QA01 accepts independent paths',()=>assert.equal(checkImpact(['src/A.cs'],['tools/chatgpt-qa/a.mjs']).status,'PASS'));
test('QA01 catches case-insensitive collisions',()=>assert.ok(checkImpact(['Docs/QA/a.md'],['docs/qa/A.md']).findings.some(f=>f.code==='FILE_COLLISION')));
test('QA01 forbids product source edits',()=>assert.ok(checkImpact([],['src/App/Foo.cs']).findings.some(f=>f.code==='OUTSIDE_OWNERSHIP')));
test('QA01 rejects traversal and rooted paths',()=>{for(const p of ['../key','/etc/passwd','C:/Windows/system.ini','a//b'])assert.throws(()=>safeRelativePath(p));});
// QA02: fail-closed static policy, separate from PR #1.
test('QA02 accepts current read-only policy',()=>assert.equal(checkSecuritySource(makeFlags()).status,'PASS'));
test('QA02 rejects a flag changed to true',()=>assert.ok(checkSecuritySource(makeFlags().replace('MigrationEnabled = false','MigrationEnabled = true')).findings.some(f=>f.code==='UNSAFE_AV3_FLAG')));
test('QA02 rejects missing or duplicated flags',()=>{
  assert.equal(checkSecuritySource(makeFlags().replace('MigrationEnabled = false','OtherFlag = false')).status,'FAIL');
  assert.equal(checkSecuritySource(makeFlags()+'\npublic const bool MigrationEnabled = false;').status,'FAIL');
});
test('QA02 rejects a production authorized status',()=>assert.equal(checkSecuritySource(makeFlags().replace('ReadOnlyValidated','ProductionAuthorized')).status,'FAIL'));
// QA03: Node built-in runner exercises all dedicated checks without .NET or dependencies.
test('QA03 fixtures are deterministic JSON',()=>assert.doesNotThrow(()=>JSON.parse(local('./fixtures/diagnostic-cases.json'))));
// QA04: evaluate measurements conservatively.
test('QA04 exact median for odd and even samples',()=>{assert.equal(median([3,1,2]),2);assert.equal(median([20,10,40,30]),25);});
test('QA04 detects allocation reduction while refusing speed claims',()=>{
  const rows=[100,110,105].map((v,i)=>metrics(v,'baseline',i)).concat([60,70,65].map((v,i)=>metrics(v,'modified',i)));
  const q=analyzePerformance(rows);
  assert.equal(q.status,'PASS');assert.ok(q.details.metrics.AllocatedBytes.pctChange<0);assert.equal(q.details.speedClaimAllowed,false);
});
test('QA04 detects changed identity call count',()=>{const rows=[...Array(3)].map((_,i)=>metrics(100,'baseline',i)).concat([...Array(3)].map((_,i)=>metrics(90,'modified',i)));
  rows[3].Output.Phases[0].Calls=54;assert.ok(analyzePerformance(rows).findings.some(f=>f.code==='IDENTITY_CALL_REGRESSION'));
});
test('QA04 rejects fewer than three runs per arm',()=>assert.equal(analyzePerformance([metrics(100,'baseline'),metrics(90,'modified')]).status,'FAIL'));
// QA05: compare evidence and maintain NO-GO.
test('QA05 accepts consistent WO29 summaries as evidence references',()=>assert.equal(checkEvidence(evidence()).status,'PASS'));
test('QA05 detects fabricated PASS count',()=>{const e=evidence();e.regression=regression(1446,110);assert.equal(checkEvidence(e).status,'FAIL');});
test('QA05 rejects an altered report even when logs are intact',()=>{const e=evidence();e.execution=e.execution.replace('1,445 passed','1,444 passed');assert.equal(checkEvidence(e).status,'FAIL');});
test('QA05 requires consistent tested SHA',()=>{const e=evidence();e.gates.Source='f'.repeat(40);assert.ok(checkEvidence(e).findings.some(f=>f.code==='SOURCE_MISMATCH'));});
test('QA05 rejects unauthorized production gate and missing NO-GO',()=>{const e=evidence();e.gates.Output.ProductionWriterEnabled='true';e.readiness='Phase 1 GO';assert.equal(checkEvidence(e).status,'FAIL');});
// QA06: static UI triage, not an accessibility test.
test('QA06 reports candidates without certifying actual accessibility',()=>{
  const q=scanXaml({'src/App/View.xaml':'<Button Content="" Background="#FFFFFF" />'});
  assert.equal(q.status,'PARTIAL');assert.equal(q.details.actualAccessibilityVerified,false);assert.equal(q.findings.length,2);
});
test('QA06 yields NOT_TESTED without XAML',()=>assert.equal(scanXaml({}).status,'NOT_TESTED'));
// QA07: synthetic input catalog distinct from product implementation.
test('QA07 has 30 synthetics across 10 classes and 3 decisions',()=>{const q=validateDiagnosticFixtures(JSON.parse(local('./fixtures/diagnostic-cases.json')));assert.equal(q.status,'PASS');assert.equal(q.details.count,30);});
test('QA07 rejects mutation and duplicate IDs',()=>{const cases=JSON.parse(local('./fixtures/diagnostic-cases.json'));cases[0].mayMutate=true;cases[1].id=cases[0].id;assert.equal(validateDiagnosticFixtures(cases).status,'FAIL');});
// QA08: workflow policy.
test('QA08 validates focused workflow scope',()=>assert.equal(checkCiScope(local('../../.github/workflows/chatgpt-qa.yml')).status,'PASS'));
test('QA08 detects missing paths',()=>assert.equal(checkCiScope('on:\n  pull_request:\n  workflow_dispatch:\npermissions:\n  contents: read').status,'FAIL'));
// QA09: local dependency governance; not vulnerability lookup.
test('QA09 reads pinned PackageVersion attributes',()=>{const q=auditDependencies('<PackageVersion Include="A" Version="1.2.3" /><PackageVersion Include="B" Version="2.0.0" />');assert.equal(q.status,'PARTIAL');assert.equal(q.details.packages,2);});
test('QA09 rejects duplicate package names and wildcards',()=>assert.equal(auditDependencies('<PackageVersion Include="A" Version="1.*" /><PackageVersion Include="a" Version="2.0.0" />').status,'FAIL'));
// QA10: preserve explicit integration blockers.
test('QA10 remains blocked without observed remote CI',()=>{const q=premergeChecks([{id:'QA02',status:'PASS'}],{codexRefKnown:true,testExecutionObserved:true,githubActionsObserved:false});assert.equal(q.status,'BLOCKED');});
test('QA10 fails when any contributing audit failed',()=>assert.equal(premergeChecks([{id:'QA02',status:'FAIL'}],{codexRefKnown:true,testExecutionObserved:true,githubActionsObserved:true}).status,'FAIL'));
test('QA10 passes only with checks and all prerequisites',()=>assert.equal(premergeChecks([{id:'QA02',status:'PASS'}],{codexRefKnown:true,testExecutionObserved:true,githubActionsObserved:true}).status,'PASS'));

test('QA10 blocks if a required audit is partial',()=>assert.equal(premergeChecks([{id:'QA06',status:'PARTIAL'}],{codexRefKnown:true,testExecutionObserved:true,githubActionsObserved:true}).status,'BLOCKED'));
