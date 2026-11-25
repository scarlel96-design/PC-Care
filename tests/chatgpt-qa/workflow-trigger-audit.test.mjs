import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  analyzeWorkflow, auditWorkflowDirectory, classifyChangedFiles, QA_ONLY_PATHS,
} from '../../tools/chatgpt-qa/workflow-trigger-audit.mjs';

const legacy = (body = '') => 'name: Historical qualification\non:\n  push:\n    branches: [codex/old]\n  pull_request:\n' + body + '  workflow_dispatch:\njobs:\n  check:\n    runs-on: windows-latest\n';
const filtered = () => legacy('    paths-ignore:\n' + QA_ONLY_PATHS.map(path => "      - '" + path + "'").join('\n') + '\n');

test('historical unfiltered pull requests are a blocking finding', () => {
  const result = analyzeWorkflow('wo27-windows-validation.yml', legacy());
  assert.equal(result.status, 'FAIL');
  assert.equal(result.missingQaPaths.length, 4);
});
test('all narrow QA exclusions permit a historical workflow', () => {
  const result = analyzeWorkflow('wo27-windows-validation.yml', filtered());
  assert.equal(result.status, 'PASS');
  assert.equal(result.ignoredQaPaths.length, 4);
});
test('a missing exclusion fails closed', () => {
  const text = filtered().replace("      - 'docs/qa/**'\n", '');
  assert.equal(analyzeWorkflow('wo27-windows-validation.yml', text).status, 'FAIL');
});
test('nonhistorical QA workflow needs no exclusion', () => {
  assert.equal(analyzeWorkflow('chatgpt-qa.yml', "name: QA\non:\n  pull_request:\n    paths:\n      - 'tools/chatgpt-qa/**'\n").status, 'PASS');
});
test('workflow_dispatch alone is not a pull request', () => {
  const result = analyzeWorkflow('wo29-stage-vault.yml', 'on:\n  workflow_dispatch:\njobs:\n  job:\n');
  assert.equal(result.pullRequest, false);
  assert.equal(result.status, 'PASS');
});
test('inline event expressions are treated as unknown', () => {
  assert.equal(analyzeWorkflow('wo27-windows-validation.yml', 'on:\n  pull_request: {}\n').status, 'FAIL');
});
test('QA-only paths do not require legacy PR jobs', () => {
  const result = classifyChangedFiles(['docs/qa/plan.md', 'tools/chatgpt-qa/new.mjs']);
  assert.equal(result.allQaOnly, true);
  assert.equal(result.legacyPrRequired, false);
});
test('mixed changes still require product PR jobs', () => {
  assert.equal(classifyChangedFiles(['docs/qa/plan.md', 'src/App/Host.cs']).legacyPrRequired, true);
});
test('legacy workflow edits still require product PR jobs', () => {
  assert.equal(classifyChangedFiles(['.github/workflows/wo27-windows-validation.yml']).legacyPrRequired, true);
});
test('non-QA paths never match QA-only filters', () => {
  assert.equal(classifyChangedFiles(['docs/rework/WO30_REPORT.md']).allQaOnly, false);
});
test('empty changed files are invalid', () => {
  assert.throws(() => classifyChangedFiles([]));
});
test('audit requires all 26 historical workflows', () => {
  const root = mkdtempSync(join(tmpdir(), 'pc-care-ci-'));
  try {
    mkdirSync(join(root,'.github','workflows'), { recursive: true });
    writeFileSync(join(root,'.github','workflows','wo27-windows-validation.yml'), filtered());
    assert.equal(auditWorkflowDirectory(root).status, 'FAIL');
  } finally { rmSync(root, { recursive: true, force: true }); }
});
test('audit passes with 26 scoped historical workflows', () => {
  const root = mkdtempSync(join(tmpdir(), 'pc-care-ci-'));
  try {
    const dir = join(root,'.github','workflows');
    mkdirSync(dir, { recursive: true });
    for (let i = 2; i <= 27; i++) {
      const name = 'wo' + String(i).padStart(2, '0') + '-windows-validation.yml';
      writeFileSync(join(dir, name), filtered());
    }
    writeFileSync(join(dir, 'chatgpt-qa.yml'), "on:\n  pull_request:\n    paths:\n      - 'tools/chatgpt-qa/**'\n");
    const audit = auditWorkflowDirectory(root);
    assert.equal(audit.status, 'PASS');
    assert.equal(audit.historicalCount, 26);
    assert.equal(audit.qaOnlyPrFanoutAfter, 1);
  } finally { rmSync(root, { recursive: true, force: true }); }
});
