import { createHash } from 'node:crypto';
import { readFileSync, realpathSync } from 'node:fs';
import { resolve, relative, isAbsolute, sep } from 'node:path';

export const QA_TASKS = Object.freeze([
  'QA01_CHANGE_IMPACT', 'QA02_SECURITY_POLICY', 'QA03_TEST_AUTOMATION',
  'QA04_PERFORMANCE', 'QA05_EVIDENCE', 'QA06_UI_STATIC',
  'QA07_DIAGNOSTIC_FIXTURES', 'QA08_CI_SCOPE',
  'QA09_DEPENDENCY_AUDIT', 'QA10_PREMERGE',
]);
export const SECURITY_FLAGS = Object.freeze([
  'ProductionWriterEnabled', 'JournalWriterEnabled', 'MigrationEnabled',
  'WriterEnableReady', 'ExternalReviewCompleted', 'ProductionEnableAuthorized',
]);
export const ROOT = 'tools/chatgpt-qa/';
const QA_ONLY = ['tools/chatgpt-qa/', 'tests/chatgpt-qa/', 'docs/qa/', '.github/workflows/chatgpt-qa.yml'];
const HEX40 = /^[0-9a-f]{40}$/i;

function finding(id, code, message, severity = 'ERROR') { return { id, code, severity, message }; }
function result(id, details, findings = [], state = undefined) {
  return {
    id, status: state ?? (findings.some(f => f.severity === 'ERROR') ? 'FAIL' : 'PASS'),
    findings, details,
  };
}
export function sha256(data) { return createHash('sha256').update(data).digest('hex'); }

// Reject absolute paths and traversal before comparing ownership. Do not open a rejected path.
export function safeRelativePath(input) {
  if (typeof input !== 'string' || !input.trim() || input.includes('\0')) throw new Error('invalid path');
  const name = input.replaceAll('\\', '/').replace(/^\.\//, '');
  if (name.startsWith('/') || /^[a-zA-Z]:/.test(name) || name.startsWith('~')) throw new Error('absolute path');
  const parts = name.split('/');
  if (parts.some(p => !p || p === '..' || p === '.' || p.includes(':'))) throw new Error('path traversal/invalid segment');
  return parts.join('/');
}
export function safeFile(root, input) {
  const name = safeRelativePath(input);
  const full = resolve(root, ...name.split('/'));
  const rel = relative(resolve(root), full);
  if (!rel || rel.startsWith('..' + sep) || rel === '..' || isAbsolute(rel)) throw new Error('outside root');
  const canonicalRoot = realpathSync(resolve(root));
  const canonicalFile = realpathSync(full);
  const fromRealRoot = relative(canonicalRoot, canonicalFile);
  if (!fromRealRoot || fromRealRoot.startsWith('..' + sep) || fromRealRoot === '..' || isAbsolute(fromRealRoot)) throw new Error('symlink outside root');
  return full;
}
export function checkImpact(codexFiles, qaFiles) {
  const findings = [];
  const normalize = (paths, label) => {
    const items = [];
    for (const p of paths) {
      try { items.push(safeRelativePath(p)); }
      catch { findings.push(finding('QA01_CHANGE_IMPACT', 'UNSAFE_PATH', `${label}: ${p}`)); }
    }
    return items;
  };
  const a = normalize(codexFiles, 'Codex');
  const b = normalize(qaFiles, 'GPT');
  const keys = new Set(a.map(x => x.toLowerCase()));
  for (const p of b) {
    if (!QA_ONLY.some(allowed => p.toLowerCase() === allowed.toLowerCase() || (allowed.endsWith('/') && p.toLowerCase().startsWith(allowed.toLowerCase())))) {
      findings.push(finding('QA01_CHANGE_IMPACT', 'OUTSIDE_OWNERSHIP', p));
    }
    if (keys.has(p.toLowerCase())) findings.push(finding('QA01_CHANGE_IMPACT', 'FILE_COLLISION', p));
  }
  return result('QA01_CHANGE_IMPACT', { codexCount: a.length, qaCount: b.length, collisions: findings.filter(f=>f.code==='FILE_COLLISION').length }, findings);
}

// Source-text inspection only. This check does not prove runtime behavior.
export function checkSecuritySource(source) {
  const findings = [];
  for (const name of SECURITY_FLAGS) {
    const re = new RegExp(`^[ \\t]*public[ \\t]+(?:static[ \\t]+readonly|const)[ \\t]+bool[ \\t]+${name}[ \\t]*=[ \\t]*([^;\\r\\n]+)[ \\t]*;`, 'gm');
    const matches = [...source.matchAll(re)];
    if (matches.length !== 1 || matches[0][1].trim() !== 'false') {
      findings.push(finding('QA02_SECURITY_POLICY', 'UNSAFE_AV3_FLAG', `${name}: expected one literal false; found ${matches.length === 1 ? matches[0][1].trim() : matches.length + ' declarations'}`));
    }
  }
  const matches = [...source.matchAll(/^[ \t]*public[ \t]+const[ \t]+Av3ProductionStatus[ \t]+CurrentStatus[ \t]*=[ \t]*Av3ProductionStatus\.([^;\r\n]+)[ \t]*;/gm)];
  if (matches.length !== 1 || matches[0][1].trim() !== 'ReadOnlyValidated') {
    findings.push(finding('QA02_SECURITY_POLICY', 'AV3_STATUS', 'Expected one ReadOnlyValidated status'));
  }
  return result('QA02_SECURITY_POLICY', { flags: SECURITY_FLAGS.length, statusDeclarations: matches.length }, findings);
}
export function checkEvidence(record) {
  const findings = [];
  const { regression, securityLab, gates, readiness, execution } = record;
  function summary(text, kind, expected) {
    const match = text.match(/Failed:\s*(\d+)\s*,\s*Passed:\s*(\d+)\s*,\s*Skipped:\s*(\d+)\s*,\s*Total:\s*(\d+)/i);
    if (!match) { findings.push(finding('QA05_EVIDENCE', 'TEST_SUMMARY_MISSING', kind)); return null; }
    const [failed, passed, skipped, total] = match.slice(1).map(Number);
    if (failed !== expected[0] || passed !== expected[1] || skipped !== expected[2] || total !== failed + passed + skipped) {
      findings.push(finding('QA05_EVIDENCE', 'TEST_COUNTS_MISMATCH', `${kind}: ${failed}/${passed}/${skipped}/${total}`));
    }
    if (!/Exit:\s*0\b/i.test(text)) findings.push(finding('QA05_EVIDENCE', 'EXIT_NOT_ZERO', kind));
    const source = text.match(/^Source:\s*([0-9a-f]{40})\s*$/mi)?.[1];
    if (!source) findings.push(finding('QA05_EVIDENCE', 'SOURCE_UNVERIFIED', kind));
    return { failed, passed, skipped, total, source };
  }
  const app = summary(regression, 'App', [0, 1445, 111]);
  const lab = summary(securityLab, 'SecurityLab', [0, 36, 0]);
  let parsed;
  try { parsed = typeof gates === 'string' ? JSON.parse(gates) : gates; }
  catch { findings.push(finding('QA05_EVIDENCE', 'GATES_JSON_INVALID', 'invalid JSON')); }
  if (!parsed?.Output) findings.push(finding('QA05_EVIDENCE', 'GATES_MISSING', 'Production gates missing'));
  else for (const flag of SECURITY_FLAGS) if (parsed.Output[flag] !== 'false' && parsed.Output[flag] !== false) findings.push(finding('QA05_EVIDENCE', 'GATE_MISMATCH', flag));
  const sources = [app?.source, lab?.source, parsed?.Source].filter(Boolean);
  if (sources.length !== 3 || sources.some(s => s !== sources[0]) || !sources.every(s => HEX40.test(s))) {
    findings.push(finding('QA05_EVIDENCE', 'SOURCE_MISMATCH', 'Tests and gate records must use one valid source SHA'));
  }
  if (!/Phase 1 NO-GO/i.test(readiness)) findings.push(finding('QA05_EVIDENCE', 'READINESS_CONTRADICTION', 'Missing Phase 1 NO-GO'));
  if (!/PARTIAL/i.test(execution) || !/NOT TESTED|remain open|remain OPEN|remain open\./i.test(execution)) {
    findings.push(finding('QA05_EVIDENCE', 'REPORT_SCOPE', 'Missing partial/open qualification')); }
  for (const [pattern,label] of [
    [/1,445\s+passed/i,'app pass count'],
    [/36\s+passed/i,'SecurityLab pass count'],
    [/26\s+readers/i,'26 recovery interruptions'],
    [/22\s+staging interruptions/i,'22 staging interruptions'],
    [/33\s+v5 opens/i,'33 v5 vault reopenings'],
    [/132\s+authenticated file comparisons/i,'132 authenticated comparisons'],
  ]) {
    if (!pattern.test(execution)) findings.push(finding('QA05_EVIDENCE','REPORT_EVIDENCE_MISMATCH',label));
  }
  return result('QA05_EVIDENCE', { app, securityLab: lab, source: sources.length === 3 && sources.every(s=>s===sources[0]) ? sources[0] : null, hashes: null, note: 'Source references only; not independently verified Git blob hashes' }, findings);
}

export function median(values) {
  if (!Array.isArray(values) || values.length === 0 || values.some(v => !Number.isFinite(v))) throw new Error('invalid sample');
  const a = [...values].sort((x,y) => x-y), i = Math.floor(a.length/2);
  return a.length % 2 ? a[i] : (a[i-1]+a[i])/2;
}
export function analyzePerformance(rows, transactionCount = 4096) {
  const findings = [];
  const groups = { baseline: [], modified: [] };
  for (const row of rows) {
    if (row?.Transactions !== transactionCount || !Object.hasOwn(groups, row.Kind)) continue;
    if (row?.ExitCode !== 0 || !row?.Output) { findings.push(finding('QA04_PERFORMANCE', 'BAD_SAMPLE', `sequence ${row?.Sequence}`)); continue; }
    groups[row.Kind].push(row.Output);
  }
  if (groups.baseline.length < 3 || groups.modified.length < 3) findings.push(finding('QA04_PERFORMANCE', 'INSUFFICIENT_SAMPLES', 'At least three runs per arm are required'));
  const metrics = {};
  for (const field of ['AllocatedBytes','ElapsedMs','CpuMs','PeakSampledHandles']) {
    const a=groups.baseline.map(r=>r[field]),b=groups.modified.map(r=>r[field]);
    if (!a.length || !b.length || [...a,...b].some(v=>typeof v!=='number'||!Number.isFinite(v)||v<0)) { findings.push(finding('QA04_PERFORMANCE','INVALID_METRIC',field));continue; }
    const base=median(a),changed=median(b);
    metrics[field]={baseMedian:base,modifiedMedian:changed,pctChange:base===0?null:Math.round(((changed-base)/base)*10000)/100,baselineRange:[Math.min(...a),Math.max(...a)],modifiedRange:[Math.min(...b),Math.max(...b)]};
  }
  const a=groups.baseline.map(r=>r.ElapsedMs),b=groups.modified.map(r=>r.ElapsedMs);
  const overlapping= a.length&&b.length ? Math.max(Math.min(...a), Math.min(...b)) <= Math.min(Math.max(...a), Math.max(...b)) : null;
  const baseCalls=groups.baseline.map(r=>r.Phases?.find(p=>p.Name==='NativeIdentityCheck')?.Calls), modCalls=groups.modified.map(r=>r.Phases?.find(p=>p.Name==='NativeIdentityCheck')?.Calls);
  if ([...baseCalls,...modCalls].some(c=>!Number.isInteger(c)) || new Set([...baseCalls,...modCalls]).size!==1) findings.push(finding('QA04_PERFORMANCE', 'IDENTITY_CALL_REGRESSION', 'Native identity calls differ or are missing'));
  return result('QA04_PERFORMANCE', { sampleCounts:{baseline:a.length,modified:b.length}, metrics, elapsedRangesOverlap:overlapping, speedClaimAllowed:false, explanation:'Historical measurements cannot establish real-world speedup or production readiness' }, findings);
}

// Identify review candidates. Findings are not functional or accessibility failures.
export function scanXaml(files) {
  const findings=[];let count=0;
  for (const [path, content] of Object.entries(files)) {
    if (!path.toLowerCase().endsWith('.xaml')) continue;
    count++;
    const lines=content.split(/\r?\n/);
    for(let i=0;i<lines.length;i++) {
      const text=lines[i];
      if (/\b(?:Foreground|Background)\s*=\s*['"]#[0-9a-fA-F]{6,8}['"]/.test(text)) findings.push(finding('QA06_UI_STATIC','HARDCODED_COLOR',`${path}:${i+1}`,'REVIEW'));
      if (/<Button\b/.test(text) && /\bContent\s*=\s*['"]\s*['"]/.test(text) && !/AutomationProperties\.Name\s*=/.test(text)) findings.push(finding('QA06_UI_STATIC','EMPTY_BUTTON_LABEL',`${path}:${i+1}`,'REVIEW'));
    }
  }
  return result('QA06_UI_STATIC',{xamlFiles:count,reviewCandidates:findings.length,actualAccessibilityVerified:false},findings, count?'PARTIAL':'NOT_TESTED');
}
export const DIAGNOSTIC_CATEGORIES = Object.freeze(['startup','cache','disk','memory','cpu','service','windows-update','network','driver','audio']);
export const DIAGNOSTIC_STATES = Object.freeze(['ISSUE','NORMAL','INSUFFICIENT_EVIDENCE']);
export function validateDiagnosticFixtures(cases) {
  const findings=[];
  if (!Array.isArray(cases)) return result('QA07_DIAGNOSTIC_FIXTURES',{},[finding('QA07_DIAGNOSTIC_FIXTURES','INVALID_FIXTURE','Expected array')]);
  const keys=new Set(), counts={};
  for (const row of cases) {
    if (!row || typeof row.id!=='string' || keys.has(row.id)) findings.push(finding('QA07_DIAGNOSTIC_FIXTURES','DUPLICATE_ID',String(row?.id)));
    keys.add(row?.id);
    if (!DIAGNOSTIC_CATEGORIES.includes(row?.category)||!DIAGNOSTIC_STATES.includes(row?.expected)||typeof row?.observation!=='string'||row.observation.trim().length<8||typeof row?.rationale!=='string'||row.rationale.trim().length<8||row?.mayMutate!==false) {
      findings.push(finding('QA07_DIAGNOSTIC_FIXTURES','INVALID_CASE',String(row?.id)));
    }
    counts[row?.category]??={};counts[row?.category][row?.expected]=(counts[row?.category][row?.expected]||0)+1;
  }
  for(const category of DIAGNOSTIC_CATEGORIES)for(const status of DIAGNOSTIC_STATES)if(counts[category]?.[status]!==1) findings.push(finding('QA07_DIAGNOSTIC_FIXTURES','CATEGORY_COVERAGE',`${category}/${status}: require one`));
  return result('QA07_DIAGNOSTIC_FIXTURES',{count:cases.length,categories:DIAGNOSTIC_CATEGORIES.length,syntheticOnly:true,notEngineTests:true},findings);
}
export function auditDependencies(xml) {
  const findings=[], names = new Set(), packages=[];
  for(const match of xml.matchAll(/<PackageVersion\s+[^>]*?Include="([^"]+)"[^>]*?Version="([^"]+)"[^>]*?\/?\s*>/g)) {
    const name=match[1],version=match[2],key=name.toLowerCase();
    packages.push({name,version});
    if(names.has(key))findings.push(finding('QA09_DEPENDENCY_AUDIT','DUPLICATE_PACKAGE',name));
    names.add(key);
    if(/[\*]|\$\(|[\[\],()]/.test(version))findings.push(finding('QA09_DEPENDENCY_AUDIT','NON_PINNED_VERSION',`${name}: ${version}`));
  }
  if (!packages.length) findings.push(finding('QA09_DEPENDENCY_AUDIT','NO_PACKAGES','No PackageVersion entries found; may require manual review','REVIEW'));
  return result('QA09_DEPENDENCY_AUDIT',{packages:packages.length,staticOnly:true,licenseAuditPerformed:false,networkAdvisoriesChecked:false},findings,findings.some(f=>f.severity==='ERROR')?'FAIL':'PARTIAL');
}
export function checkCiScope(yaml) {
  const findings=[];
  for(const required of ['pull_request:', 'workflow_dispatch:', 'contents: read','node --test tests/chatgpt-qa/', 'node tools/chatgpt-qa/cli.mjs']) {
    if(!yaml.includes(required))findings.push(finding('QA08_CI_SCOPE','MISSING_CI_POLICY',required));
  }
  if(!yaml.includes('tools/chatgpt-qa/**')||!yaml.includes('tests/chatgpt-qa/**'))findings.push(finding('QA08_CI_SCOPE','UNSCOPED_CI','Missing independent path filters'));
  return result('QA08_CI_SCOPE',{independent:true,runObserved:false},findings);
}
export function premergeChecks(checks, {codexRefKnown, testExecutionObserved, githubActionsObserved}) {
  const failures=checks.filter(x=>x.status==='FAIL').map(x=>x.id);
  const blockers=checks.filter(x=>['BLOCKED','NOT_TESTED','PARTIAL'].includes(x.status)).map(x=>`${x.id}: ${x.status}`);
  if(!codexRefKnown)blockers.push('Codex base head not compared');
  if(!testExecutionObserved)blockers.push('Local test execution not observed');
  if(!githubActionsObserved)blockers.push('GitHub Actions result not observed');
  return result('QA10_PREMERGE',{failures,blockers,ready:failures.length===0&&blockers.length===0},[],failures.length?'FAIL':blockers.length?'BLOCKED':'PASS');
}

// Read only; no subprocess and no mutation of product files.
export function auditWo29(root) {
  const base='docs/rework/';
  const get = name => readFileSync(safeFile(root,base+name),'utf8');
  const details={};const results=[];
  const sources={
    regression:get('evidence/wo29/REGRESSION_RESULT.txt'),
    securityLab:get('evidence/wo29/SECURITYLAB_RESULT.txt'),
    gates:get('evidence/wo29/PRODUCTION_GATES.json'),
    readiness:get('WO29_PRODUCT_READINESS.md'),
    execution:get('WO29_EXECUTION_REPORT.md'),
  };
  results.push(checkEvidence(sources));
  const source=readFileSync(safeFile(root,'src/SmartPerformanceDoctor.AstraVault/Target/Av3PhaseGate.cs'),'utf8');
  results.push(checkSecuritySource(source));
  const perf=JSON.parse(get('evidence/wo29/HISTORY_ALLOCATION_RESULT.json'));
  const analysis = analyzePerformance(perf.Rows);
  const performanceDocument = get('WO29_HISTORY_ALLOCATION.md');
  if (analysis.details.metrics.AllocatedBytes && !performanceDocument.includes(String(analysis.details.metrics.AllocatedBytes.pctChange) + '%')) {
    analysis.status = 'FAIL';
    analysis.findings.push(finding('QA04_PERFORMANCE', 'PERFORMANCE_REPORT_MISMATCH', 'Computed allocation reduction differs from report'));
  }
  results.push(analysis);
  const fixtures=JSON.parse(readFileSync(safeFile(root,'tests/chatgpt-qa/fixtures/diagnostic-cases.json'),'utf8'));
  results.push(validateDiagnosticFixtures(fixtures));
  const props=readFileSync(safeFile(root,'Directory.Packages.props'),'utf8');
  results.push(auditDependencies(props));
  const workflow=readFileSync(safeFile(root,'.github/workflows/chatgpt-qa.yml'),'utf8');
  results.push(checkCiScope(workflow));
  return {sourceRef:'WO29 evidence in current local checkout', results, status:results.some(r=>r.status==='FAIL')?'FAIL':'PARTIAL', note:'No Windows UI, VM, signing, or runtime tests performed by this audit'};
}
