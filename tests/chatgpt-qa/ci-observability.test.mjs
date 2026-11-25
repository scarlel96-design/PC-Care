import test from 'node:test';
import assert from 'node:assert/strict';
import { analyzeSnapshot, parseRepository, parseHeadSha, fetchGithubSnapshot, validateSnapshotShape } from '../../tools/chatgpt-qa/ci-observability.mjs';
import { readOptions } from '../../tools/chatgpt-qa/ci-observability-cli.mjs';

const sha = 'a'.repeat(40);
const iso = seconds => new Date(Date.UTC(2026, 10, 25, 0, 0, seconds)).toISOString();
const run = (id, extra = {}) => ({
  id, workflow_id: 101, name: 'Windows qualification', head_sha: sha,
  event: 'pull_request', status: 'completed', conclusion: 'success', run_attempt: 1, ...extra,
});
const job = (id, seconds, extra = {}) => ({
  id, status: 'completed', conclusion: 'success', created_at: iso(0),
  started_at: iso(5), completed_at: iso(5 + seconds), ...extra,
});
const completeSample = () => ({ total_count: 2,
  workflow_runs: [run(1), run(2, { workflow_id: 102, name: 'Independent QA' })],
  jobs_by_run: { 1: { total_count: 1, jobs: [job(10, 120)] }, 2: { total_count: 1, jobs: [job(20, 60)] } },
});

test('completed jobs give observed wall time rather than billable time', () => {
  const result = analyzeSnapshot(completeSample());
  assert.equal(result.status, 'COMPLETE');
  assert.equal(result.counts.runs, 2);
  assert.equal(result.counts.successes, 2);
  assert.equal(result.jobTiming.completedJobWallSecondsObserved, 180);
  assert.equal(result.jobTiming.completedJobWallMinutesObserved, 3);
  assert.equal(result.jobTiming.completedJobSecondsMedian, 90);
  assert.equal(result.jobTiming.queueWaitSecondsMedian, 5);
  assert.equal(result.jobTiming.billedMinutes, null);
  assert.equal(result.fanoutByCommit[0].distinctWorkflows, 2);
});

test('missing job detail results in PARTIAL not false zero', () => {
  const data = completeSample();
  delete data.jobs_by_run[2];
  const report = analyzeSnapshot(data);
  assert.equal(report.status, 'PARTIAL');
  assert.equal(report.coverage.runsMissingJobDetails, 1);
  assert.equal(report.jobTiming.completedJobWallSecondsObserved, 120);
});

test('no jobs gives unknown observed runtime', () => {
  const report = analyzeSnapshot({ workflow_runs: [run(1)] });
  assert.equal(report.status, 'PARTIAL');
  assert.equal(report.jobTiming.completedJobWallMinutesObserved, null);
});

test('incomplete paginated runs are marked PARTIAL', () => {
  const data = completeSample(); data.total_count = 3;
  const result = analyzeSnapshot(data);
  assert.equal(result.status, 'PARTIAL');
  assert.ok(result.limitations.some(s => s.includes('Run list incomplete')));
});

test('incomplete jobs collection is not treated as complete', () => {
  const data = completeSample(); data.jobs_by_run[1].total_count = 2;
  assert.equal(analyzeSnapshot(data).status, 'PARTIAL');
});

test('active runs never count as success or failure', () => {
  const data = completeSample(); data.workflow_runs[0].status = 'queued'; data.workflow_runs[0].conclusion = null;
  const result = analyzeSnapshot(data);
  assert.equal(result.counts.active, 1);
  assert.equal(result.counts.successes, 1);
});

test('repeated run groups are candidates, not automatic wasted-run claims', () => {
  const data = completeSample(); data.workflow_runs[1].workflow_id = 101;
  const report = analyzeSnapshot(data);
  assert.equal(report.repeatCandidates.extraRuns, 1);
  assert.deepEqual(report.repeatCandidates.groups[0].runIds, [1, 2]);
});

test('explicit run_attempt counts rerun attempts separately', () => {
  const data = completeSample(); data.workflow_runs[1].run_attempt = 3;
  assert.equal(analyzeSnapshot(data).repeatCandidates.rerunAttempts, 2);
});

test('mixed status counts separate cancellations from failures', () => {
  const data = completeSample(); data.workflow_runs[1].conclusion = 'cancelled';
  const report = analyzeSnapshot(data);
  assert.equal(report.counts.failures, 0);
  assert.equal(report.counts.cancelled, 1);
});

test('duplicate input run IDs do not inflate fanout', () => {
  const data = completeSample(); data.workflow_runs.push(run(1));
  const report = analyzeSnapshot(data);
  assert.equal(report.counts.runs, 2);
  assert.equal(report.status, 'PARTIAL');
});

test('invalid or reversed job timestamps cannot produce negative durations', () => {
  const data = completeSample(); data.jobs_by_run[1].jobs[0].completed_at = iso(0);
  const report = analyzeSnapshot(data);
  assert.equal(report.status, 'PARTIAL');
  assert.equal(report.jobTiming.completedJobWallSecondsObserved, 60);
  assert.ok(report.limitations.some(x => x.includes('invalid or absent timestamps')));
});

test('no workflow_runs must fail early', () => {
  assert.throws(() => analyzeSnapshot({ workflow_runs: null }), TypeError);
});

test('invalid job record collection must fail early', () => {
  assert.throws(() => analyzeSnapshot({ workflow_runs: [], jobs_by_run: [] }), TypeError);
});

test('GitHub repository validation rejects injection-like paths', () => {
  for (const value of ['owner/repo/other', 'owner%2Frepo', 'owner', '', '../repo']) {
    assert.throws(() => parseRepository(value));
  }
  assert.equal(parseRepository('scarlel96-design/PC-Care'), 'scarlel96-design/PC-Care');
});

test('invalid SHA and count are rejected', async () => {
  assert.throws(() => parseHeadSha('HEAD'));
  assert.equal(parseHeadSha(sha.toUpperCase()), sha);
  await assert.rejects(() => fetchGithubSnapshot({ repository: 'owner/repo', sha, maxRuns: 201 }), RangeError);
});

test('offline fixture validation rejects oversized result', () => {
  assert.throws(() => validateSnapshotShape({ workflow_runs: Array.from({ length: 201 }, (_, i) => run(i)) }));
});

test('CLI enforces exactly one data source', () => {
  assert.throws(() => readOptions([]));
  assert.throws(() => readOptions(['--input', 'data.json', '--repository', 'o/r', '--sha', sha]));
  assert.deepEqual(readOptions(['--input', 'data.json']).input, 'data.json');
});

test('CLI rejects missing SHA and repeated arguments', () => {
  assert.throws(() => readOptions(['--repository', 'a/b']));
  assert.throws(() => readOptions(['--input', 'a.json', '--input', 'b.json']));
  assert.throws(() => readOptions(['--input']));
});

test('bounded GitHub API acquisition and job fetching through mock', async () => {
  const called = [];
  const fetchImpl = async (url, init) => {
    called.push({ url, headers: init.headers });
    if (url.includes('/jobs?')) return { ok: true, json: async () => ({ total_count: 1, jobs: [job(7, 10)] }) };
    return { ok: true, json: async () => ({ total_count: 1, workflow_runs: [run(1)] }) };
  };
  const data = await fetchGithubSnapshot({ repository: 'owner/repo', sha, withJobs: true, fetchImpl });
  assert.equal(data.workflow_runs.length, 1);
  assert.equal(analyzeSnapshot(data).status, 'COMPLETE');
  assert.equal(called.length, 2);
  assert.ok(called.every(x => x.url.startsWith('https://api.github.com/repos/owner/repo/')));
  assert.ok(called.every(x => !Object.keys(x.headers).includes('Authorization')));
});

test('GitHub API errors are not converted into a fabricated zero-run result', async () => {
  await assert.rejects(() => fetchGithubSnapshot({ repository: 'owner/repo', sha, fetchImpl: async () => ({ ok: false, status: 403 }) }), /HTTP 403/);
});

test('partial snapshot can still preserve real completed-job totals', () => {
  const d = completeSample(); d.workflow_runs.push(run(3, { status: 'in_progress', conclusion: null }));
  const x = analyzeSnapshot(d);
  assert.equal(x.jobTiming.completedJobWallSecondsObserved, 180);
  assert.equal(x.status, 'PARTIAL');
});

test('comparison distinguishes count changes from billed savings', async () => {
  const { compareSnapshots } = await import('../../tools/chatgpt-qa/ci-observability.mjs');
  const before = completeSample();
  const after = { ...completeSample(), workflow_runs: [run(1)], total_count: 1,
    jobs_by_run: { 1: { total_count: 1, jobs: [job(10, 120)] } } };
  const comparison = compareSnapshots(before, after);
  assert.equal(comparison.differences.workflowRunCount, -1);
  assert.equal(comparison.differences.observedJobWallSeconds, -60);
  assert.equal(comparison.differences.billedMinutesSaved, null);
  assert.match(comparison.caveat, /controlled experiments/);
});

test('incomplete comparisons never assert saved wall time', async () => {
  const { compareSnapshots } = await import('../../tools/chatgpt-qa/ci-observability.mjs');
  const partial = { workflow_runs: [run(1)], total_count: 1 };
  const result = compareSnapshots(completeSample(), partial);
  assert.equal(result.status, 'PARTIAL');
  assert.equal(result.differences.observedJobWallSeconds, null);
});

test('comparison command requires offline reference input', () => {
  assert.throws(() => readOptions(['--repository', 'owner/repo', '--sha', sha, '--compare', 'test.json']));
  assert.equal(readOptions(['--input', 'before.json', '--compare', 'after.json']).compare, 'after.json');
});

test('failed steps can be grouped without reading job logs', () => {
  const data = completeSample();
  data.jobs_by_run[1].jobs[0].steps = [
    { name: 'Restore', conclusion: 'success' },
    { name: 'Release build', conclusion: 'failure' },
  ];
  data.jobs_by_run[2].jobs[0].steps = [
    { name: 'Release build', conclusion: 'failure' },
  ];
  assert.deepEqual(analyzeSnapshot(data).failedStepCounts, { 'Release build': 2 });
});

test('active job details cannot produce complete status', () => {
  const data = completeSample();
  data.jobs_by_run[1].jobs[0].status = 'queued';
  assert.equal(analyzeSnapshot(data).status, 'PARTIAL');
});

test('completed run without conclusion remains incomplete', () => {
  const data = completeSample(); data.workflow_runs[0].conclusion = null;
  assert.equal(analyzeSnapshot(data).status, 'PARTIAL');
});

test('archived GitHub job summary is internally consistent', async () => {
  const { readFileSync } = await import('node:fs');
  const base = JSON.parse(readFileSync(new URL('../../docs/qa/CI_RUN_BASELINE_20261125.json', import.meta.url), 'utf8'));
  const seconds = base.runs.reduce((total, item) => total + item.observed_job_wall_seconds, 0);
  assert.equal(seconds, 3552);
  assert.equal(base.job_wall_seconds, seconds);
  assert.equal(base.counts.runs, base.runs.length);
  assert.equal(base.counts.failed_runs, base.runs.filter(item => item.conclusion === 'failure').length);
  assert.equal(base.runs.length, new Set(base.runs.map(item => item.run_id)).size);
  assert.equal(base.billed_minutes, null);
});
