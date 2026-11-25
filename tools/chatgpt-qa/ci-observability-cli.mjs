#!/usr/bin/env node
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { analyzeSnapshot, compareSnapshots, fetchGithubSnapshot, validateSnapshotShape } from './ci-observability.mjs';

export function readOptions(argv) {
  const allowed = new Set(['--input', '--repository', '--sha', '--max-runs', '--with-jobs', '--output', '--compare']);
  const parsed = {};
  for (let i = 0; i < argv.length; i++) {
    const key = argv[i];
    if (!allowed.has(key)) throw new Error(`Unknown option ${key}`);
    if (key === '--with-jobs') {
      if (parsed.withJobs) throw new Error('Duplicate --with-jobs');
      parsed.withJobs = true;
    } else {
      if (hasOwn(parsed, key)) throw new Error(`Duplicate option ${key}`);
      if (!argv[i + 1] || argv[i + 1].startsWith('--')) throw new Error(`${key} requires a value`);
      parsed[key] = argv[++i];
    }
  }
  if (Boolean(parsed['--input']) === Boolean(parsed['--repository'])) {
    throw new Error('Specify exactly one of --input or --repository');
  }
  if (parsed['--repository'] && !parsed['--sha']) throw new Error('--sha required with --repository');
  if (parsed['--compare'] && !parsed['--input']) throw new Error('--compare requires --input');
  if (parsed['--input'] && (parsed['--sha'] || parsed['--max-runs'] || parsed.withJobs)) {
    throw new Error('--input cannot be combined with live GitHub options');
  }
  const maxRuns = parsed['--max-runs'] ? Number(parsed['--max-runs']) : 100;
  return { input: parsed['--input'], repository: parsed['--repository'], sha: parsed['--sha'],
    maxRuns, withJobs: parsed.withJobs ?? false, output: parsed['--output'], compare: parsed['--compare'] };
}
const hasOwn = (obj, key) => Object.prototype.hasOwnProperty.call(obj, key);

export async function main(argv) {
  const options = readOptions(argv);
  let snapshot;
  if (options.input) {
    snapshot = JSON.parse(readFileSync(resolve(options.input), 'utf8'));
    validateSnapshotShape(snapshot);
  } else {
    snapshot = await fetchGithubSnapshot({
      repository: options.repository, sha: options.sha, maxRuns: options.maxRuns,
      withJobs: options.withJobs, token: process.env.GITHUB_TOKEN,
    });
  }
  const report = options.compare
    ? compareSnapshots(snapshot, JSON.parse(readFileSync(resolve(options.compare), 'utf8')))
    : analyzeSnapshot(snapshot);
  const json = JSON.stringify(report, null, 2) + '\n';
  if (options.output) writeFileSync(resolve(options.output), json, 'utf8');
  else process.stdout.write(json);
  return report;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2)).catch(error => {
    console.error(`CI_ANALYSIS_ERROR: ${error.message}`);
    process.exitCode = 2;
  });
}
