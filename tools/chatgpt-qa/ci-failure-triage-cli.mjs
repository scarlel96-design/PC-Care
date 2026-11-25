#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseJobLog, summarizeFailureEvidence } from './ci-failure-triage.mjs';

// Input is a local log or evidence manifest. Never fetch remote credentials.
export function main(args) {
  const command = args[0];
  const path = args[1];
  if (!path || args.length !== 2) throw new Error('Usage: ci-failure-triage-cli.mjs log <file> | evidence <json-file>');
  const text = readFileSync(resolve(path), 'utf8');
  if (command === 'log') return parseJobLog(text);
  if (command === 'evidence') return summarizeFailureEvidence(JSON.parse(text));
  throw new Error(`Unknown command: ${command}`);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  try {
    const result = main(process.argv.slice(2));
    console.log(JSON.stringify(result, null, 2));
    if (result.status === 'PARTIAL') process.exitCode = 1;
  } catch (error) {
    console.error(`CI_FAILURE_AUDIT_ERROR: ${error.message}`);
    process.exitCode = 2;
  }
}
