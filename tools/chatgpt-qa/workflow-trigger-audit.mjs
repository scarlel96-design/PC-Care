/** Audit the proposed historical workflow scope. Do not modify any workflow. */
import { readFileSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { safeRelativePath } from './core.mjs';
export const QA_ONLY_PATHS=Object.freeze(['tools/chatgpt-qa/**','tests/chatgpt-qa/**','docs/qa/**','.github/workflows/chatgpt-qa.yml']);
export const HISTORICAL_WORKFLOWS=Object.freeze(Array.from({length:26},(_,i)=>{const n=i+2;return `wo${String(n).padStart(2,'0')}-${n===2?'windows-security':n===3?'windows-updater':'windows-validation'}.yml`;}));
function prSection(source) {
  const lines=source.split(/\r?\n/).filter(x=>!/^\s*#/.test(x)&&x.trim());
  const roots=lines.flatMap((x,i)=>/^on:\s*$/.test(x)?[i]:[]);
  if(roots.length!==1)return {present:false,unknown:true,ignores:[]};
  const body=[];for(let i=roots[0]+1;i<lines.length&&!/^\S/.test(lines[i]);i++)body.push(lines[i]);
  const starts=body.flatMap((x,i)=>/^  pull_request:/.test(x)?[i]:[]);
  if(!starts.length)return {present:false,unknown:false,ignores:[]};
  if(starts.length!==1||!/^  pull_request:\s*$/.test(body[starts[0]]))return {present:true,unknown:true,ignores:[]};
  const section=[];for(let i=starts[0]+1;i<body.length&&!/^  \S/.test(body[i]);i++)section.push(body[i]);
  if(!section.length)return {present:true,unknown:false,ignores:[]};
  const ignores=[];
  if(!/^    paths-ignore:\s*$/.test(section[0]))return {present:true,unknown:true,ignores};
  for(const line of section.slice(1)){
    const m=line.match(/^      -\s+(?:'([^']+)'|"([^"]+)"|([^#'"\s]+))\s*(?:#.*)?$/);
    if(!m)return {present:true,unknown:true,ignores};
    ignores.push(m[1]??m[2]??m[3]);
  }
  return {present:true,unknown:false,ignores};
}
export function analyzeWorkflow(name,source){
  const historical=HISTORICAL_WORKFLOWS.includes(name),event=prSection(source);
  const missing=QA_ONLY_PATHS.filter(p=>!event.ignores.includes(p));
  const extra=event.ignores.filter(p=>!QA_ONLY_PATHS.includes(p));
  return {name,historical,pullRequest:event.present,ignoredQaPaths:event.ignores,missingQaPaths:historical?missing:[],extraIgnoredPaths:extra,status:historical&&(!event.present||event.unknown||missing.length||extra.length||new Set(event.ignores).size!==event.ignores.length)?'FAIL':'PASS',staticOnly:true};
}
export function classifyChangedFiles(changed,ignored=QA_ONLY_PATHS){
  if(!Array.isArray(changed)||!changed.length)throw Error('Expected nonempty changed paths');
  if(!Array.isArray(ignored)||ignored.length!==QA_ONLY_PATHS.length||new Set(ignored).size!==ignored.length||ignored.some(p=>!QA_ONLY_PATHS.includes(p)))throw Error('Unapproved exclusion');
  const paths=changed.map(safeRelativePath);if(new Set(paths).size!==paths.length)throw Error('Duplicate paths');
  const allQaOnly=paths.every(file=>ignored.some(p=>p.endsWith('/**')?file.startsWith(p.slice(0,-2)):file===p));
  return {allQaOnly,legacyPrRequired:!allQaOnly};
}
export function auditWorkflowDirectory(root){
  const dir=join(resolve(root),'.github','workflows');const names=readdirSync(dir).filter(x=>/\.ya?ml$/.test(x)).sort();
  const entries=names.map(n=>analyzeWorkflow(n,readFileSync(join(dir,n),'utf8')));
  const missingInventory=HISTORICAL_WORKFLOWS.filter(n=>!names.includes(n));
  const unexpectedHistorical=names.filter(n=>/^wo(0[2-9]|1[0-9]|2[0-7])-/.test(n)&&!HISTORICAL_WORKFLOWS.includes(n));
  const historical=entries.filter(e=>e.historical),failing=historical.filter(e=>e.status==='FAIL');
  const otherPr=entries.filter(e=>e.pullRequest&&!e.historical).length;
  const pass=!missingInventory.length&&!unexpectedHistorical.length&&!failing.length;
  return {status:pass?'PASS':'FAIL',workflowCount:names.length,historicalCount:historical.length,expectedLegacy:26,missingInventory,unexpectedHistorical,missing:failing.map(e=>({name:e.name,missing:e.missingQaPaths,extra:e.extraIgnoredPaths})),qaOnlyPrFanoutBefore:historical.filter(e=>e.pullRequest).length+otherPr,qaOnlyPrFanoutAfter:pass?otherPr:null,entries,requiredChecksVerified:false,modelOnly:true};
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){try{const a=auditWorkflowDirectory(process.argv[2]??'.');console.log(JSON.stringify(a,null,2));if(a.status!=='PASS')process.exitCode=1;}catch(e){console.error(e.message);process.exitCode=2;}}
