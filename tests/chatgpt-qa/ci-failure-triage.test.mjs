import test from 'node:test';
import assert from 'node:assert/strict';
import { parseJobLog, summarizeFailureEvidence } from '../../tools/chatgpt-qa/ci-failure-triage.mjs';

const line = '2026-11-25T07:44:03.000Z ##[error]D:\\a\\PC-Care\\PC-Care\\src\\SmartPerformanceDoctor.App\\Services\\MacIconCatalog.cs(10,5): error CsWinRT1032: collection expression not supported';
const mkRun = (runId, sdk = '10.0.404') => ({
  runId, jobId: 10000 + runId, conclusion: 'failure', sdk,
  logSummary: { emittedErrorLines: 2, uniqueLocations: 1, codes: { CsWinRT1032: 2 } },
});

test('extracts CsWinRT1032 from GitHub job log', () => {
  const r = parseJobLog(line);
  assert.equal(r.codes.CsWinRT1032, 1);
  assert.equal(r.diagnostics[0].file, 'src/SmartPerformanceDoctor.App/Services/MacIconCatalog.cs');
});
test('deduplicates repeated compiler diagnostics', () => {
  const r = parseJobLog(`${line}\n${line}`);
  assert.equal(r.uniqueLocations, 1);
  assert.equal(r.emittedErrorLines, 2);
  assert.equal(r.diagnostics[0].occurrences, 2);
});
test('preserves line and column precisely', () => {
  const r = parseJobLog(line);
  assert.equal(r.diagnostics[0].line, 10);
  assert.equal(r.diagnostics[0].column, 5);
});
test('handles additional error families', () => {
  const r = parseJobLog(`${line}\nD:\\a\\PC-Care\\PC-Care\\src\\Foo.cs(1,2): error CS1002: ; expected`);
  assert.equal(r.codes.CS1002, 1);
  assert.equal(r.uniqueLocations, 2);
});
test('counts errors without file locations separately', () => {
  const r = parseJobLog('error NETSDK1005: missing assets');
  assert.equal(r.emittedErrorLines, 1);
  assert.equal(r.unlocatedErrorLines, 1);
});
test('extracts observed installed SDK version', () => {
  const r = parseJobLog("dotnet-install: .NET Core SDK with version '10.0.404' is already installed.");
  assert.deepEqual(r.sdkVersions, ['10.0.404']);
});
test('extracts pinned installed SDK version', () => {
  const r = parseJobLog('dotnet-install: Installed version is 10.0.402');
  assert.deepEqual(r.sdkVersions, ['10.0.402']);
});
test('empty log is valid with no errors', () => {
  assert.equal(parseJobLog('').emittedErrorLines, 0);
});
test('invalid log type fails closed', () => {
  assert.throws(() => parseJobLog(null), TypeError);
});
test('groups the same compiler diagnostic across failed runs', () => {
  const r = summarizeFailureEvidence({ expectedFailures: 2, runs: [mkRun(1), mkRun(2)] });
  assert.equal(r.status, 'COMPLETE');
  assert.deepEqual(r.commonToAllFailures, ['CsWinRT1032']);
  assert.equal(r.codeGroups[0].emittedLines, 4);
});
test('missing job/log summaries are partial', () => {
  const x = mkRun(1); delete x.logSummary;
  assert.equal(summarizeFailureEvidence({ expectedFailures: 1, runs: [x] }).status, 'PARTIAL');
});
test('duplicate run identifiers are flagged', () => {
  const r = summarizeFailureEvidence({ expectedFailures: 1, runs: [mkRun(1), mkRun(1)] });
  assert.equal(r.status, 'PARTIAL');
});
test('reported failure count mismatch is partial', () => {
  assert.equal(summarizeFailureEvidence({expectedFailures:2,runs:[mkRun(1)]}).status, 'PARTIAL');
});
test('does not infer SDK difference as proven cause', () => {
  const r = summarizeFailureEvidence({ expectedFailures: 1, runs: [mkRun(1), {runId:3,conclusion:'success',sdk:'10.0.402'}] });
  assert.equal(r.sdkComparison.versionsDiffer, true);
  assert.equal(r.sdkComparison.causeVerified, false);
});
test('incomplete and in-progress run remains partial', () => {
  const r = summarizeFailureEvidence({ expectedFailures: 1, runs: [mkRun(1), {runId:4,conclusion:null}] });
  assert.equal(r.status,'PARTIAL');
});
test('non-integer run IDs rejected', () => {
  assert.throws(() => summarizeFailureEvidence({ runs: [{runId:'one'}] }),TypeError);
});

test('inconsistent error totals fail closed', () => {
  const x = mkRun(1); x.logSummary.emittedErrorLines = 5;
  assert.equal(summarizeFailureEvidence({ expectedFailures: 1, runs: [x] }).status, 'PARTIAL');
});
test('one successful baseline does not reduce failed-run count', () => {
  const r = summarizeFailureEvidence({expectedFailures:1,runs:[mkRun(1),{runId:2,conclusion:'success',sdk:'10.0.402'}]});
  assert.equal(r.failedRuns,1);
  assert.equal(r.runsSupplied,2);
});
