#!/usr/bin/env node
/** Inspect repository SDK selectors without editing source, workflows, or CI secrets. */
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { auditDirectory, inspectRunLog, summarizeSdkComparisons } from './sdk-drift-audit.mjs';

export function runCli(args) {
  const [command, ...rest] = args;
  const get = flag => { const i = rest.indexOf(flag); return i < 0 ? null : rest[i+1]; };
  switch (command) {
    case 'audit': {
      const baselinePath = get('--baseline');
      const baseline = baselinePath ? JSON.parse(readFileSync(resolve(baselinePath), 'utf8')) : null;
      return auditDirectory(get('--root') ?? '.', baseline);
    }
    case 'log': {
      const path = get('--file'); if (!path) throw Error('Use log --file <local-job-log>');
      return inspectRunLog(readFileSync(resolve(path), 'utf8'));
    }
    case 'compare': {
      const path = get('--evidence'); if (!path) throw Error('Use compare --evidence <observations.json>');
      return summarizeSdkComparisons(JSON.parse(readFileSync(resolve(path), 'utf8')).observations);
    }
    default: throw Error('Commands: audit [--root .] [--baseline JSON] | log --file LOG | compare --evidence JSON');
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  try { const result = runCli(process.argv.slice(2)); console.log(JSON.stringify(result, null, 2)); if (result.status === 'BLOCKED') process.exitCode = 1; }
  catch (error) { console.error(`SDK_AUDIT_ERROR: ${error.message}`); process.exitCode = 2; }
}
