import { readFileSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const QA_ONLY_PATHS = Object.freeze([
  'tools/chatgpt-qa/**',
  'tests/chatgpt-qa/**',
  'docs/qa/**',
  '.github/workflows/chatgpt-qa.yml',
]);

const HISTORICAL_WO = /^wo(0[2-9]|1[0-9]|2[0-7])-.*\.ya?ml$/;

function eventSection(source, event) {
  if (typeof source !== 'string') throw new TypeError('Expected workflow text');
  const lines = source.split(/\r?\n/);
  const root = lines.findIndex(line => /^on:\s*(?:#.*)?$/.test(line));
  if (root === -1) return { present: false, unknown: true, ignores: [] };
  let start = -1;
  for (let i = root + 1; i < lines.length; i++) {
    if (/^\S/.test(lines[i]) && !/^#/.test(lines[i])) break;
    if (new RegExp('^  ' + event + ':\\s*(?:#.*)?$').test(lines[i])) { start = i; break; }
    if (new RegExp('^  ' + event + ':\\s*\\S').test(lines[i])) return { present: true, unknown: true, ignores: [] };
  }
  if (start === -1) return { present: false, unknown: false, ignores: [] };
  const block = [];
  for (let i = start + 1; i < lines.length; i++) {
    if (/^  \S/.test(lines[i]) || (/^\S/.test(lines[i]) && !/^#/.test(lines[i]))) break;
    block.push(lines[i]);
  }
  const ignoreIndex = block.findIndex(line => /^    paths-ignore:\s*(?:#.*)?$/.test(line));
  if (ignoreIndex === -1) return { present: true, unknown: false, ignores: [] };
  const ignores = [];
  for (let i = ignoreIndex + 1; i < block.length; i++) {
    const line = block[i];
    if (/^    \S/.test(line)) break;
    const found = line.match(/^      -\s+['"]?([^'"#\r\n]+?)['"]?\s*(?:#.*)?$/);
    if (found) ignores.push(found[1].trim());
  }
  return { present: true, unknown: false, ignores };
}

export function analyzeWorkflow(name, source) {
  const event = eventSection(source, 'pull_request');
  const historical = HISTORICAL_WO.test(name);
  const missing = event.present ? QA_ONLY_PATHS.filter(path => !event.ignores.includes(path)) : [];
  return {
    name, historical, pullRequest: event.present,
    ignoredQaPaths: event.ignores,
    missingQaPaths: missing,
    status: event.unknown || (historical && (!event.present || missing.length)) ? 'FAIL' : 'PASS',
  };
}

export function classifyChangedFiles(changed, ignored = QA_ONLY_PATHS) {
  if (!Array.isArray(changed) || changed.length === 0) throw new Error('Expected nonempty changed paths');
  const match = (file, pattern) => pattern.endsWith('/**')
    ? file.startsWith(pattern.slice(0, -2))
    : file === pattern;
  const allQaOnly = changed.every(file => ignored.some(pattern => match(file, pattern)));
  return { allQaOnly, legacyPrRequired: !allQaOnly };
}

export function auditWorkflowDirectory(root) {
  const dir = join(resolve(root), '.github', 'workflows');
  const files = readdirSync(dir).filter(name => /\.ya?ml$/.test(name)).sort();
  const entries = files.map(name => analyzeWorkflow(name, readFileSync(join(dir, name), 'utf8')));
  const historical = entries.filter(entry => entry.historical);
  const failing = entries.filter(entry => entry.status === 'FAIL');
  const otherPr = entries.filter(entry => entry.pullRequest && !entry.historical);
  const legacyPr = historical.filter(entry => entry.pullRequest);
  return {
    status: failing.length || historical.length !== 26 ? 'FAIL' : 'PASS',
    workflowCount: files.length,
    historicalCount: historical.length,
    qaOnlyPrFanoutBefore: legacyPr.length + otherPr.length,
    qaOnlyPrFanoutAfter: otherPr.length,
    missing: failing.map(e => ({ name: e.name, missing: e.missingQaPaths })),
    expectedLegacy: 26,
    entries,
  };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const root = process.argv[2] ?? '.';
    const audit = auditWorkflowDirectory(root);
    console.log(JSON.stringify(audit, null, 2));
    if (audit.status !== 'PASS') process.exitCode = 1;
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
