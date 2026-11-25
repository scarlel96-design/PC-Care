#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { auditWo29, checkImpact, premergeChecks, scanXaml, safeFile, sha256 } from './core.mjs';

// Commands only read the current checkout or explicitly supplied file lists.
const [command, ...argv] = process.argv.slice(2);
const option = flag => { const index=argv.indexOf(flag);return index<0?null:argv[index+1]; };
const root=resolve(option('--root') ?? '.');
function textFile(p){return readFileSync(safeFile(root,p),'utf8');}
function lines(input){return input.split(/\r?\n/).map(v=>v.trim()).filter(Boolean);}
function output(value){console.log(JSON.stringify(value,null,2));if(value.status==='FAIL'||value.status==='BLOCKED')process.exitCode=1;}
try {
  switch(command){
    case 'wo29': output(auditWo29(root));break;
    case 'impact': {
      const codex=option('--codex-files'),qa=option('--qa-files');
      if(!codex||!qa)throw Error('impact requires --codex-files and --qa-files');
      output(checkImpact(lines(textFile(codex)),lines(textFile(qa))));break;
    }
    case 'ui': {
      const paths=option('--paths');if(!paths)throw Error('ui requires --paths file');
      const files={};for(const p of lines(textFile(paths)))files[p]=textFile(p);
      output(scanXaml(files));break;
    }
    case 'premerge': {
      const checks=option('--checks');if(!checks)throw Error('premerge requires --checks file');
      const data=JSON.parse(textFile(checks));
      output(premergeChecks(data.results,{codexRefKnown:data.codexRefKnown===true,testExecutionObserved:data.testExecutionObserved===true,githubActionsObserved:data.githubActionsObserved===true}));break;
    }
    case 'hash': {
      const path=option('--file');if(!path)throw Error('hash requires --file');
      const bytes=readFileSync(safeFile(root,path));
      console.log(JSON.stringify({file:path,sha256:sha256(bytes),bytes:bytes.length},null,2));break;
    }
    default: throw Error('Commands: wo29 | impact | ui | premerge | hash. Use --root to set checkout.');
  }
} catch(error) {console.error(`QA_ERROR: ${error.message}`);process.exitCode=2;}
