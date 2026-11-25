#!/usr/bin/env node
/** Read local checkout and supplied logs. Do not write to product files. */
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { auditCheckout, compareBuildEnvironments, createBaseline, parseJobLog } from './build-env-provenance.mjs';

export function main(args) {
  const [command,...rest]=args;
  const arg=key=>{const pos=rest.indexOf(key);return pos===-1?null:rest[pos+1];};
  if(command==='audit'){
    const baseline=arg('--baseline')?JSON.parse(readFileSync(resolve(arg('--baseline')),'utf8')):null;
    return auditCheckout(arg('--root')??'.',baseline);
  }
  if(command==='baseline')return createBaseline(auditCheckout(arg('--root')??'.'));
  if(command==='log'){
    const file=arg('--file');if(!file)throw Error('Missing --file');
    return parseJobLog(readFileSync(resolve(file),'utf8'));
  }
  if(command==='compare'){
    const file=arg('--file');if(!file)throw Error('Missing --file');
    const data=JSON.parse(readFileSync(resolve(file),'utf8'));
    if(!data?.left||!data?.right)throw Error('Comparison needs left/right observations');
    return compareBuildEnvironments(data.left,data.right);
  }
  throw Error('Usage: audit [--root . --baseline file] | baseline [--root .] | log --file JOBLOG | compare --file JSON');
}
if(process.argv[1]&&fileURLToPath(import.meta.url)===resolve(process.argv[1])){
  try{const r=main(process.argv.slice(2));console.log(JSON.stringify(r,null,2));if(r.status==='REVIEW')process.exitCode=1;}
  catch(e){console.error('BUILD_ENV_AUDIT_ERROR: '+e.message);process.exitCode=2;}
}
