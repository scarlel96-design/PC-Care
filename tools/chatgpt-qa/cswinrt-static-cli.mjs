#!/usr/bin/env node
import { readFileSync, readdirSync, realpathSync, lstatSync } from 'node:fs';
import { resolve, relative, join, isAbsolute, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { scanFiles } from './cswinrt-static.mjs';

const MAX_FILE_BYTES = 2 * 1024 * 1024;
const MAX_FILE_COUNT = 10000;
const EXCLUDE = new Set(['.git', 'node_modules', 'bin', 'obj', '.vs', 'packages']);

function guardedPath(root, input) {
  if (typeof input !== 'string' || !input.trim() || isAbsolute(input) || /^[A-Za-z]:[\\/]/.test(input) || input.startsWith('\\\\'))
    throw new Error('Expected a repository-relative path');
  const joined = resolve(root, input);
  const rel = relative(root, joined);
  if (rel === '..' || rel.startsWith('..'+sep) || isAbsolute(rel)) throw new Error(`Path escapes repository: ${input}`);
  const target = realpathSync(joined);
  const relReal = relative(root, target);
  if (relReal === '..' || relReal.startsWith('..'+sep) || isAbsolute(relReal)) throw new Error(`Symlink target escapes repository: ${input}`);
  return target;
}

function collectFiles(root, start) {
  const found=[];
  function walk(path) {
    const loc=guardedPath(root,path);
    const meta=lstatSync(loc);
    if (meta.isSymbolicLink()) throw new Error(`Symlink not supported: ${path}`);
    if (meta.isFile()) {
      if (!loc.endsWith('.cs')) return;
      if (meta.size > MAX_FILE_BYTES) throw new Error(`C# file exceeds size limit: ${path}`);
      found.push(relative(root,loc).replaceAll(sep,'/'));
      if (found.length>MAX_FILE_COUNT) throw new Error('Too many source files');
    } else if (meta.isDirectory()) {
      for (const entry of readdirSync(loc,{withFileTypes:true})) {
        if (EXCLUDE.has(entry.name)) continue;
        if (entry.isSymbolicLink()) continue;
        if (entry.isFile() && !entry.name.endsWith('.cs')) continue;
        walk(join(path,entry.name));
      }
    }
  }
  walk(start);
  return found.sort();
}

export function collectSource(root, { start='src/SmartPerformanceDoctor.App', filesList=null } = {}) {
  root=realpathSync(resolve(root));
  let paths;
  if (filesList) {
    paths=readFileSync(guardedPath(root,filesList),'utf8').split(/\r?\n/).map(x=>x.trim()).filter(Boolean);
    if (paths.length>MAX_FILE_COUNT) throw new Error('Too many listed source files');
    for(const p of paths) if (!p.endsWith('.cs')) throw new Error(`Listed path is not .cs: ${p}`);
  } else paths=collectFiles(root,start);
  const files={};
  for (const path of [...new Set(paths)]) {
    const f=guardedPath(root,path); const stat=lstatSync(f);
    if (!stat.isFile() || stat.size>MAX_FILE_BYTES) throw new Error(`Invalid file size or type: ${path}`);
    const key=relative(root,f).replaceAll(sep,'/');
    files[key]=readFileSync(f,'utf8');
  }
  return files;
}

function fingerprint(d) { return `${d.path}:${d.line}:${d.column}:${d.rule}`; }
export function compareReports(before,after) {
  const all = x => {
    if (!x || !Array.isArray(x.results)) throw new TypeError('Invalid scan report');
    return x.results.flatMap(r => r.findings || []).filter(x=>x.confidence==='HIGH');
  };
  const existing=new Set(all(before).map(fingerprint));
  const next=all(after).filter(d=>!existing.has(fingerprint(d)));
  return { newHighConfidence:next.length, newFindings:next, conservativeLineBasedComparison:true };
}

export function run(args) {
  let root='.',start='src/SmartPerformanceDoctor.App',filesList=null,baseline=null,failHigh=false,failNew=false,summary=false;
  for(let i=0;i<args.length;i++) {
    const flag=args[i];
    if (['--root','--start','--files-list','--baseline'].includes(flag)) {
      if (!args[i+1]) throw Error(`Missing value: ${flag}`);
      const value=args[++i];
      if(flag==='--root') root=value;
      if(flag==='--start') start=value;
      if(flag==='--files-list') filesList=value;
      if(flag==='--baseline') baseline=value;
    } else if(flag==='--fail-high') failHigh=true;
    else if(flag==='--fail-new') failNew=true;
    else if(flag==='--summary') summary=true;
    else throw Error(`Unknown argument: ${flag}`);
  }
  if(failNew&&!baseline) throw Error('--fail-new requires --baseline');
  const absoluteRoot=realpathSync(resolve(root));
  const report=scanFiles(collectSource(absoluteRoot,{start,filesList}));
  if(baseline) report.comparison=compareReports(JSON.parse(readFileSync(guardedPath(absoluteRoot,baseline),'utf8')),report);
  const exitCode=(failHigh&&report.highConfidence)||(failNew&&report.comparison.newHighConfidence)?1:0;
  const view=summary?{...report,results:undefined}:report;
  return {report:view,exitCode};
}

if(process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const {report,exitCode}=run(process.argv.slice(2));
    console.log(JSON.stringify(report,null,2));process.exitCode=exitCode;
  } catch(err){console.error(`CSWINRT_SCAN_ERROR: ${err.message}`);process.exitCode=2;}
}
