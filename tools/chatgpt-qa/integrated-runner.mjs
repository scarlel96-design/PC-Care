#!/usr/bin/env node
/** Run the independent QA checks. Do not change product files or release gates. */
import { createHash } from 'node:crypto';
import { readFileSync, existsSync, lstatSync, readdirSync, realpathSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

export const SCHEMA = 'pc-care-independent-qa-integration-v1';
export const CHECK_IDS = Object.freeze([
  'unit-tests', 'ci-failure-evidence', 'sdk-run-compare', 'build-env-run-compare',
  'ci-observability-synthetic', 'wo29-source-evidence', 'sdk-workflow-drift',
  'build-env-checkout', 'cswinrt-source-guard', 'workflow-trigger-scope',
  'change-impact', 'ui-static-review', 'premerge-readiness',
]);
const STATUS = new Set(['PASS','WARN','BLOCKED','SKIPPED','ERROR']);
const VALID_PROFILES = new Set(['offline','checkout']);
const TOOL_DIR = 'tools/chatgpt-qa';
const TEST_DIR = 'tests/chatgpt-qa';
const REQUIRED_TESTS = Object.freeze(['build-env-provenance','ci-failure-triage','ci-observability','core','cswinrt-patterns','cswinrt-static-cli','cswinrt-static','integrated-runner','sdk-drift-audit','adversarial-regression','hardening-boundaries'].map(n=>n+'.test.mjs'));
const TEST_BASELINE_MINIMUM=344;
function verifyTestInventory(root,item,details) {
  const missing=REQUIRED_TESTS.filter(n=>!item.args.some(p=>p.replaceAll('\\','/').endsWith('/'+n)));
  if(missing.length||details.total<TEST_BASELINE_MINIMUM)return {missing,reason:'Required test inventory or minimum test count missing'};
  try {
    const manifest=JSON.parse(readFileSync(relativeFile(root,'docs/qa/QA_TEST_INVENTORY_WO30.json'),'utf8'));
    if(manifest.schema!==1||!Number.isSafeInteger(manifest.minimumTests)||manifest.minimumTests<TEST_BASELINE_MINIMUM||details.total<manifest.minimumTests||!manifest.files||Object.keys(manifest.files).length!==REQUIRED_TESTS.length)throw Error('Invalid test inventory');
    for(const name of REQUIRED_TESTS){const path=join(TEST_DIR,name);const hash=createHash('sha256').update(readFileSync(relativeFile(root,path),'utf8').replace(/\r\n/g,'\n')).digest('hex');if(manifest.files[name]!==hash)throw Error('Required test hash mismatch: '+name);}
    return null;
  } catch(error) {return {missing,reason:error.message};}
}

export function relativeFile(root, rel, { mustExist=true, allowOutput=false }={}) {
  if (typeof rel !== 'string' || !rel.trim() || /\0/.test(rel) || isAbsolute(rel) || /^[A-Za-z]:[\\/]/.test(rel) || rel.startsWith('\\\\')) {
    throw new Error('Expected a nonempty repository-relative path');
  }
  if (rel.split(/[\\/]/).some(part=>part==='..' || part==='.' || part==='')) throw new Error('Unsafe relative path');
  const absolute=resolve(root,rel);
  const r=relative(root,absolute);
  if (!r || r==='..' || r.startsWith('..'+sep) || isAbsolute(r)) throw new Error('Path escapes repository');
  if (mustExist) {
    if (!existsSync(absolute) || !lstatSync(absolute).isFile()) throw new Error(`Required file missing: ${rel}`);
    const real=realpathSync(absolute);
    const fromRoot=relative(root,real);
    if (fromRoot==='..' || fromRoot.startsWith('..'+sep) || isAbsolute(fromRoot)) throw new Error('File symlink leaves repository');
  } else if (allowOutput) {
    if (!rel.replaceAll('\\','/').startsWith('docs/qa/')) throw new Error('Output must be inside docs/qa/');
    const parent=realpathSync(dirname(absolute));
    const fromRoot=relative(root,parent);
    if (fromRoot==='..' || fromRoot.startsWith('..'+sep) || isAbsolute(fromRoot)) throw new Error('Output parent leaves repository');
    if (existsSync(absolute)) throw new Error('Refuse nonregular or symlink output, or overwrite of existing evidence');
  }
  return absolute;
}

export function parseArgs(argv) {
  const opts={ profile:'offline', root:'.', strict:false, list:false, only:null,
    'codex-files':null, 'qa-files':null, 'ui-paths':null, 'premerge-checks':null,
    'source-sha':null, 'output':null };
  const seen=new Set();
  const values=new Set(['--root','--profile','--only','--codex-files','--qa-files','--ui-paths','--premerge-checks','--source-sha','--output']);
  for(let i=0;i<argv.length;i++) {
    const a=argv[i];
    if(a==='--strict' || a==='--list') { const key=a.slice(2); if(opts[key]) throw Error(`Duplicate ${a}`);opts[key]=true;continue; }
    if(!values.has(a)) throw new Error(`Unknown option: ${a}`);
    const key=a.slice(2),next=argv[++i];
    if(!next || next.startsWith('--') || seen.has(key)) throw Error(`Missing or duplicate value: ${a}`);
    seen.add(key); opts[key]=next;
  }
  if(!VALID_PROFILES.has(opts.profile)) throw Error('Profile must be offline or checkout');
  if(Boolean(opts['codex-files'])!==Boolean(opts['qa-files'])) throw Error('Specify both --codex-files and --qa-files');
  if(opts['source-sha'] && !/^[a-f0-9]{40}$/i.test(opts['source-sha'])) throw Error('Source SHA must be exactly 40 hexadecimal digits');
  if(opts.only!==null) {
    const vals=opts.only.split(',').map(x=>x.trim());
    if(!vals.length || vals.some(x=>!CHECK_IDS.includes(x)) || new Set(vals).size!==vals.length) throw Error('Invalid or duplicate check ID in --only');
    opts.only=vals;
  }
  return opts;
}

export function makePlan(root, opts) {
  if (!VALID_PROFILES.has(opts.profile)) throw Error('Invalid profile');
  const p=(name)=>join(TOOL_DIR,name);
  const work=join(root,'.github','workflows');
  const full=opts.profile==='checkout';
  const entries=[];
  const add=(id,title,tool,args,evidence,enabled=true,reason=null,kind='json')=>{
    entries.push({id,title,tool,args,evidence,kind,eligible:Boolean(enabled),reason:enabled?null:reason});
  };
  const testPath=join(root,TEST_DIR);
  const tests=existsSync(testPath)?readdirSync(testPath).filter(n=>n.endsWith('.test.mjs')).sort():[];
  add('unit-tests','Independent QA unit tests',null,tests.map(n=>join(TEST_DIR,n)), 'LOCAL_EXECUTION', true,null, 'tests');
  add('ci-failure-evidence','Historical 26-job failure summary',p('ci-failure-triage-cli.mjs'),['evidence','docs/qa/CI_FAILURE_EVIDENCE_WO30.json'],'HISTORICAL_GITHUB_SUMMARY');
  add('sdk-run-compare','Observed installed SDK comparison',p('sdk-drift-cli.mjs'),['compare','--evidence','docs/qa/SDK_DRIFT_RUN_EVIDENCE_WO30.json'],'HISTORICAL_GITHUB_SUMMARY');
  add('build-env-run-compare','Observed build environment comparison',p('build-env-cli.mjs'),['compare','--file','docs/qa/BUILD_ENV_COMPARE_WO23_WO30.json'],'HISTORICAL_GITHUB_SUMMARY');
  add('ci-observability-synthetic','CI aggregation parser fixture',p('ci-observability-cli.mjs'),['--input','tests/chatgpt-qa/fixtures/ci-synthetic-snapshot.json'],'SYNTHETIC_FIXTURE');
  const hasWorkflows=existsSync(work)&&readdirSync(work).filter(n=>/\.ya?ml$/.test(n)).length>=30;
  const hasSource=existsSync(join(root,'src','SmartPerformanceDoctor.App'));
  const hasEvidence=existsSync(join(root,'docs','rework','evidence','wo29','REGRESSION_RESULT.txt'));
  add('wo29-source-evidence','WO-29 security and evidence check',p('cli.mjs'),['wo29','--root','.'],'LOCAL_CHECKOUT',full&&hasSource&&hasEvidence,'Requires checkout with WO-29 evidence and product source');
  add('sdk-workflow-drift','SDK workflow baseline comparison',p('sdk-drift-cli.mjs'),['audit','--root','.','--baseline','docs/qa/SDK_DRIFT_BASELINE_WO30.json'],'LOCAL_CHECKOUT',full&&hasWorkflows,'Requires complete GitHub workflow checkout');
  add('build-env-checkout','Project and workflow package configuration',p('build-env-cli.mjs'),['audit','--root','.'],'LOCAL_CHECKOUT',full&&hasSource&&hasWorkflows,'Requires complete product source and workflows');
  add('cswinrt-source-guard','CsWinRT1032 source risk scan',p('cswinrt-static-cli.mjs'),['--root','.','--summary'],'LOCAL_CHECKOUT',full&&hasSource,'Requires C# source checkout');
  const trigger=existsSync(join(root,TOOL_DIR,'workflow-trigger-audit.mjs'));
  add('workflow-trigger-scope','Legacy workflow PR path scope',p('workflow-trigger-audit.mjs'),['.'],'OPTIONAL_BRANCH_TOOL',full&&trigger&&hasWorkflows,'PR #3 workflow scope tool not present, or checkout incomplete');
  add('change-impact','Codex versus GPT changed-file ownership',p('cli.mjs'),opts['codex-files']&&opts['qa-files']?['impact','--root','.','--codex-files',opts['codex-files'],'--qa-files',opts['qa-files']]:[],'USER_SUPPLIED',Boolean(opts['codex-files']),'Requires both changed-file lists');
  add('ui-static-review','XAML static review',p('cli.mjs'),opts['ui-paths']?['ui','--root','.','--paths',opts['ui-paths']]:[],'USER_SUPPLIED',Boolean(opts['ui-paths']),'Requires UI path list');
  add('premerge-readiness','Ten-task integration premerge gate',p('cli.mjs'),opts['premerge-checks']?['premerge','--root','.','--checks',opts['premerge-checks']]:[],'USER_SUPPLIED',Boolean(opts['premerge-checks']),'Requires approved ten-task evidence inputs');
  return entries.map(e=>opts.only && !opts.only.includes(e.id)?{...e,eligible:false,reason:'Not selected by --only'}:e);
}

export function classifyOutcome(id, exitCode, payload) {
  if(!Number.isInteger(exitCode) || exitCode<0 || exitCode>1) return {status:'ERROR',reason:`Unexpected tool exit ${String(exitCode)}`};
  if(!payload || typeof payload!=='object') return {status:'ERROR',reason:'Tool did not emit a JSON result'};
  if(id==='cswinrt-source-guard') {
    if(exitCode!==0) return {status:'BLOCKED',reason:'CsWinRT guard nonzero exit'};
    if(!Number.isSafeInteger(payload.highConfidence)) return {status:'ERROR',reason:'CsWinRT report lacks count'};
    if (!Number.isSafeInteger(payload.scannedFiles)||payload.scannedFiles<=0||!Number.isSafeInteger(payload.advisory)||payload.advisory<0||payload.highConfidence<0) return {status:'WARN',reason:'Missing or invalid scan coverage'};
    if (payload.highConfidence||payload.advisory||payload.status!=='CLEAR') return {status:'WARN',reason:'Static candidates or incomplete scan require compiler validation'};
    return {status:'PASS'};
  }
  if(id==='build-env-run-compare' && payload.status==='REVIEW' && exitCode===1)
    return {status:'WARN',reason:'Known SDK difference needs controlled reproduction'};
  const tool=payload.status;
  if(id==='premerge-readiness'&&tool==='PASS')return {status:'BLOCKED',reason:'Self-reported premerge status cannot authorize merge'};
  const states={INVENTORIED:'WARN',NOT_TESTED:'WARN',PASS:'PASS',COMPLETE:'PASS',MATCH:'PASS',ADVISORY:'WARN',PARTIAL:'WARN',INCOMPLETE:'WARN',REVIEW:id==='build-env-checkout'?'BLOCKED':'WARN',BLOCKED:'BLOCKED',FAIL:'BLOCKED',ERROR:'ERROR'};
  if(!Object.hasOwn(states,tool)) return {status:'ERROR',reason:`Unknown or absent tool status: ${String(tool)}`};
  const mapped=states[tool];
  if((exitCode===1 && !['BLOCKED','WARN'].includes(mapped)) || (exitCode===0 && mapped==='BLOCKED' && id!=='premerge-readiness'))
    return {status:'ERROR',reason:'Tool exit code disagrees with status'};
  return {status:mapped,reason:mapped==='WARN'?`Tool status ${tool}; does not establish product GO`:mapped==='BLOCKED'?`Tool status ${tool}`:null};
}

export function executeNode(root, entry, timeoutMs=120000) {
  const args=entry.kind==='tests'?['--test',...entry.args]:[entry.tool,...entry.args];
  const env={...process.env,NODE_OPTIONS:'',GITHUB_TOKEN:'',GH_TOKEN:''}; delete env.NODE_TEST_CONTEXT;
  const out=spawnSync(process.execPath,args,{cwd:root,encoding:'utf8',timeout:timeoutMs,maxBuffer:8*1024*1024,shell:false,env});
  if(out.error) return {exitCode:2,stdout:out.stdout||'',stderr:String(out.error.message)};
  return {exitCode:out.status===null?2:out.status,stdout:out.stdout||'',stderr:out.stderr||''};
}

function parseTestSummary(stdout,exitCode) {
  const value=(label)=>{const m=[...stdout.matchAll(new RegExp('^# '+label+' (\\d+)$','gm'))];return m.length===1&&Number.isSafeInteger(Number(m[0][1]))?Number(m[0][1]):null;};
  const total=value('tests'),passed=value('pass'),failed=value('fail'),skipped=value('skipped');
  if([total,passed,failed,skipped].some(x=>x===null)||total!==passed+failed+skipped) return {status:'ERROR',reason:'TAP summary missing or inconsistent',details:{total,passed,failed,skipped}};
  if(exitCode===0 && failed===0 && skipped===0) return {status:'PASS',details:{total,passed,failed,skipped}};
  if(exitCode!==0 && failed>0) return {status:'BLOCKED',reason:'QA tests failed',details:{total,passed,failed,skipped}};
  return {status:'WARN',reason:'QA tests contain skipped cases or unexpected exit',details:{total,passed,failed,skipped}};
}

export function summarize(results) {
  const counts=Object.fromEntries([...STATUS].map(x=>[x,0]));
  for(const r of results) {if(!STATUS.has(r.status)) throw Error(`Unexpected suite status: ${r.status}`);counts[r.status]++;}
  const status=!results.length?'BLOCKED':counts.BLOCKED||counts.ERROR?'BLOCKED':counts.SKIPPED?'PARTIAL':counts.WARN?'REVIEW':'PASS';
  return {status,counts,executed:results.length-counts.SKIPPED,total:results.length};
}

export function runSuite(root,opts,{executor=executeNode}={}) {
  const absolute=realpathSync(resolve(root));
  const checkPaths=['codex-files','qa-files','ui-paths','premerge-checks'];
  for(const key of checkPaths) if(opts[key]) relativeFile(absolute,opts[key]);
  const plan=makePlan(absolute,opts);
  const results=[];
  for(const item of plan) {
    if(!item.eligible) {results.push({id:item.id,title:item.title,status:'SKIPPED',reason:item.reason,evidence:item.evidence});continue;}
    try {
      if(item.kind==='tests') {
        if(!item.args.length) {results.push({id:item.id,title:item.title,status:'BLOCKED',evidence:item.evidence,reason:'No required test files',details:{total:0,passed:0,failed:0,skipped:0}});continue;}
        for(const path of item.args) relativeFile(absolute,path);
        if(REQUIRED_TESTS.every(n=>item.args.some(p=>p.replaceAll('\\','/').endsWith('/'+n)))) {
          const finding=verifyTestInventory(absolute,item,{total:Number.MAX_SAFE_INTEGER});
          if(finding){results.push({id:item.id,title:item.title,status:'BLOCKED',evidence:item.evidence,reason:finding.reason,details:{missing:finding.missing,execution:'NOT_RUN'}});continue;}
        }
      }
      if(item.kind==='json') {
        relativeFile(absolute,item.tool);
        for(const arg of item.args) if(typeof arg==='string' && /^(docs|tests)\//.test(arg)) relativeFile(absolute,arg);
      }
      const raw=executor(absolute,item);
      let state,details;
      if(item.kind==='tests') {
        const info=parseTestSummary(raw.stdout,raw.exitCode);state=info;details=info.details;
        if(info.status==='PASS') { const finding=verifyTestInventory(absolute,item,details); if(finding) { state={status:'BLOCKED',reason:finding.reason};details={...details,missing:finding.missing}; } }
      } else {
        let value;
        try {value=JSON.parse(raw.stdout);}catch{value=null;}
        state=classifyOutcome(item.id,raw.exitCode,value);
        details={toolStatus:value?.status??null};
        if(item.id==='sdk-run-compare' && value)details={...details,sdkVersionsDiffer:value.sdkVersionsDiffer??null,causeVerified:value.causeVerified===true};
        if(item.id==='ci-failure-evidence' && value)details={...details,failedRuns:value.failedRuns,commonToAllFailures:value.commonToAllFailures};
        if(item.id==='build-env-run-compare' && value)details={...details,differences:value.differences?.length??null,causeVerified:value.causeVerified===true};
        if(item.id==='cswinrt-source-guard'&&value)details={...details,highConfidence:value.highConfidence};
      }
      results.push({id:item.id,title:item.title,status:state.status,evidence:item.evidence,exitCode:raw.exitCode,reason:state.reason||null,details});
    } catch(error) {results.push({id:item.id,title:item.title,status:'ERROR',evidence:item.evidence,reason:error.message});}
  }
  const summary=summarize(results);
  return {schema:SCHEMA,sourceSha:opts['source-sha']??null,profile:opts.profile,checkSelection:opts.only??'all',status:summary.status,summary,checks:results,
    releaseDecision:'NOT_ASSESSED',productPhase1Go:false,remoteCiObservedByThisRun:false,
    limitations:['PASS indicates only independent QA checks, not Windows product qualification.',
      'Historical GitHub summaries are not live re-fetched by this offline runner.',
      'SYNTHETIC_FIXTURE results are synthetic and cannot establish production behavior.',
      'Codex conflict, UI, and premerge checks need explicit evidence inputs.',
      'No signing, reboot, UAC, interactive Windows, or AV3 production authorization is claimed.']};
}

export function main(argv,{executor=executeNode}={}) {
  const opts=parseArgs(argv),root=realpathSync(resolve(opts.root));
  if(opts.list) return {schema:SCHEMA,checks:makePlan(root,opts).map(({id,title,eligible,reason})=>({id,title,eligible,reason}))};
  const result=runSuite(root,opts,{executor});
  if(opts.output) {
    const path=relativeFile(root,opts.output,{mustExist:false,allowOutput:true});
    writeFileSync(path,JSON.stringify(result,null,2)+'\n',{encoding:'utf8',flag:'wx'});
  }
  return result;
}

if(process.argv[1] && resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {
    const opts=parseArgs(process.argv.slice(2));
    const result=main(process.argv.slice(2));
    console.log(JSON.stringify(result,null,2));
    if(result.status==='BLOCKED' || (opts.strict && result.status!=='PASS')) process.exitCode=1;
  } catch(error) {console.error(`INTEGRATED_QA_ERROR: ${error.message}`);process.exitCode=2;}
}
