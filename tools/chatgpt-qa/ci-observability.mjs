/** Read-only analysis of GitHub Actions runs and their job timestamps. */

const completed = status => status === 'completed';

function validDate(value) {
  if (typeof value !== 'string' || !value) return null;
  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) ? timestamp : null;
}

function durationSeconds(start, end) {
  const first = validDate(start);
  const last = validDate(end);
  if (first === null || last === null || last < first) return null;
  return (last - first) / 1000;
}

function median(values) {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

function asNonnegativeInteger(value) {
  return Number.isSafeInteger(value) && value >= 0 ? value : null;
}

function normalizeJobs(raw) {
  if (Array.isArray(raw)) return { jobs: raw, total_count: raw.length };
  if (!raw || !Array.isArray(raw.jobs)) return null;
  return { jobs: raw.jobs, total_count: asNonnegativeInteger(raw.total_count) };
}

/**
 * Analyze one complete or explicitly incomplete REST snapshot.
 * No inference of GitHub's billed minutes or actual CPU execution time is made.
 */
export function analyzeSnapshot(snapshot) {
  if (!snapshot || !Array.isArray(snapshot.workflow_runs)) {
    throw new TypeError('Expected workflow_runs array');
  }
  const allRuns = snapshot.workflow_runs;
  const apiTotal = asNonnegativeInteger(snapshot.total_count);
  const jobsByRun = snapshot.jobs_by_run ?? {};
  if (!jobsByRun || typeof jobsByRun !== 'object' || Array.isArray(jobsByRun)) {
    throw new TypeError('jobs_by_run must be an object');
  }
  const runIds = new Set();
  const seen = [];
  const problems = [];
  for (const run of allRuns) {
    if (!Number.isSafeInteger(run?.id)) throw new TypeError('Run id must be an integer');
    if (runIds.has(run.id)) {
      problems.push(`Duplicate run id ${run.id} in input; ignored after first occurrence`);
      continue;
    }
    runIds.add(run.id);
    seen.push(run);
  }
  if (apiTotal !== null && apiTotal > seen.length) {
    problems.push(`Run list incomplete: ${seen.length} of ${apiTotal} available`);
  }
  if (snapshot.truncated === true) problems.push('Acquisition reported truncated results');

  const counts = { runs: seen.length, completed: 0, active: 0, successes: 0, failures: 0, cancelled: 0, skipped: 0 };
  const byEvent = {};
  const byConclusion = {};
  const fanout = new Map();
  const repeated = new Map();
  let rerunAttempts = 0;
  let jobCoverage = 0;
  let fetchedJobCount = 0;
  let completedJobCount = 0;
  let activeJobCount = 0;
  let timedJobCount = 0;
  let summedJobSeconds = 0;
  const jobTimes = [];
  const queueWaits = [];
  const perWorkflow = new Map();
  const failedSteps = {};

  for (const run of seen) {
    const workflow = String(run.workflow_id ?? run.name ?? 'unknown');
    const workflowName = typeof run.name === 'string' ? run.name : workflow;
    const sha = typeof run.head_sha === 'string' ? run.head_sha : 'unknown';
    const event = typeof run.event === 'string' ? run.event : 'unknown';
    const current = String(run.status ?? 'unknown');
    const conclusion = String(run.conclusion ?? 'unknown');
    byEvent[event] = (byEvent[event] ?? 0) + 1;
    byConclusion[conclusion] = (byConclusion[conclusion] ?? 0) + 1;
    if (completed(current)) {
      counts.completed += 1;
      if (conclusion === 'unknown') problems.push(`Completed run ${run.id} has no conclusion`);
      if (conclusion === 'success') counts.successes++;
      if (conclusion === 'failure') counts.failures++;
      if (conclusion === 'cancelled') counts.cancelled++;
      if (conclusion === 'skipped') counts.skipped++;
    } else {
      counts.active++;
    }
    const groupKey = `${sha}\u0000${event}`;
    if (!fanout.has(groupKey)) fanout.set(groupKey, { sha, event, count: 0, workflows: new Set() });
    const group = fanout.get(groupKey);
    group.count++;
    group.workflows.add(workflow);

    const repeatKey = `${workflow}\u0000${sha}\u0000${event}`;
    if (!repeated.has(repeatKey)) repeated.set(repeatKey, { name: workflowName, sha, event, ids: [] });
    repeated.get(repeatKey).ids.push(run.id);
    if (Number.isSafeInteger(run.run_attempt) && run.run_attempt > 1) {
      rerunAttempts += run.run_attempt - 1;
    }

    const details = normalizeJobs(jobsByRun[String(run.id)]);
    if (!details) continue;
    jobCoverage++;
    if (details.total_count === null || details.jobs.length < details.total_count) {
      problems.push(`Jobs incomplete for run ${run.id}`);
    }
    const workflowStats = perWorkflow.get(workflow) ?? { workflowId: workflow, name: workflowName, jobs: 0, timedJobs: 0, jobWallSeconds: 0 };
    perWorkflow.set(workflow, workflowStats);
    for (const job of details.jobs) {
      fetchedJobCount++;
      if (Array.isArray(job.steps)) {
        for (const step of job.steps) {
          if (step?.conclusion !== 'failure') continue;
          const name = typeof step.name === 'string' && step.name.trim() ? step.name.trim() : '(unnamed step)';
          failedSteps[name] = (failedSteps[name] ?? 0) + 1;
        }
      }
      if (!completed(job.status)) { activeJobCount++; continue; }
      completedJobCount++;
      const duration = durationSeconds(job.started_at, job.completed_at);
      if (duration !== null) {
        timedJobCount++;
        summedJobSeconds += duration;
        jobTimes.push(duration);
        workflowStats.timedJobs++;
        workflowStats.jobWallSeconds += duration;
      } else {
        problems.push(`Completed job ${job.id ?? '?'} has invalid or absent timestamps`);
      }
      workflowStats.jobs++;
      const wait = durationSeconds(job.created_at, job.started_at);
      if (wait !== null) queueWaits.push(wait);
    }
  }

  const duplicates = [...repeated.values()].filter(group => group.ids.length > 1)
    .map(group => ({ workflow: group.name, sha: group.sha, event: group.event, runIds: group.ids, extraRuns: group.ids.length - 1 }));
  const duplicateExtraRuns = duplicates.reduce((total, group) => total + group.extraRuns, 0);
  const fanoutByCommit = [...fanout.values()].map(group => ({
    headSha: group.sha, event: group.event, runCount: group.count, distinctWorkflows: group.workflows.size,
  }));
  if (counts.active > 0) problems.push(`${counts.active} runs have not completed; final wall time is unknown`);
  if (activeJobCount > 0) problems.push(`${activeJobCount} jobs have not completed; final job wall time is unknown`);
  const coverage = { acquiredRuns: seen.length, availableRuns: apiTotal, runsWithJobDetails: jobCoverage,
    runsMissingJobDetails: seen.length - jobCoverage, fetchedJobs: fetchedJobCount,
    activeJobs: activeJobCount, completedJobs: completedJobCount, jobsWithTimestamps: timedJobCount };
  if (seen.length - jobCoverage > 0) problems.push(`${seen.length - jobCoverage} runs lack job detail; job time is partial`);
  const complete = problems.length === 0 && (apiTotal === null || seen.length === apiTotal);

  return {
    status: complete ? 'COMPLETE' : 'PARTIAL',
    limitations: problems,
    coverage,
    counts,
    eventCounts: byEvent,
    conclusionCounts: byConclusion,
    fanoutByCommit,
    repeatCandidates: { extraRuns: duplicateExtraRuns, groups: duplicates, rerunAttempts },
    jobTiming: {
      completedJobWallSecondsObserved: timedJobCount ? summedJobSeconds : null,
      completedJobWallMinutesObserved: timedJobCount ? +(summedJobSeconds / 60).toFixed(3) : null,
      completedJobSecondsMedian: median(jobTimes),
      queueWaitSecondsMedian: median(queueWaits),
      activeOrUntimedJobsExcluded: true,
      billedMinutes: null,
    },
    failedStepCounts: failedSteps,
    workflowTiming: [...perWorkflow.values()].sort((a, b) => b.jobWallSeconds - a.jobWallSeconds),
  };
}

/** Compare two observations without claiming causal savings. */
export function compareSnapshots(before, after) {
  const a = analyzeSnapshot(before);
  const b = analyzeSnapshot(after);
  const observed = a.status === 'COMPLETE' && b.status === 'COMPLETE'
    && a.jobTiming.completedJobWallSecondsObserved !== null
    && b.jobTiming.completedJobWallSecondsObserved !== null;
  return {
    status: observed ? 'COMPLETE' : 'PARTIAL',
    before: a, after: b,
    differences: {
      workflowRunCount: b.counts.runs - a.counts.runs,
      observedJobWallSeconds: observed
        ? b.jobTiming.completedJobWallSecondsObserved - a.jobTiming.completedJobWallSecondsObserved : null,
      billedMinutesSaved: null,
    },
    caveat: 'Different commits or change sets are not controlled experiments. These values do not prove CI savings.',
  };
}

/** Strict repository validation prevents arbitrary URLs and non-GitHub network targets. */
export function parseRepository(value) {
  if (typeof value !== 'string' || !/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(value) || value.split('/').some(part => part === '.' || part === '..')) {
    throw new Error('Repository must be owner/repo');
  }
  return value;
}

export function parseHeadSha(value) {
  if (typeof value !== 'string' || !/^[a-fA-F0-9]{40}$/.test(value)) throw new Error('Expected 40-character Git SHA');
  return value.toLowerCase();
}

async function requestGithub(path, options = {}) {
  const token = options.token;
  const authorization = typeof token === 'string' && token.length ? { Authorization: `Bearer ${token}` } : {};
  const res = await (options.fetchImpl ?? fetch)(`https://api.github.com${path}`, {
    headers: { Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28',
      'User-Agent': 'pc-care-read-only-ci-analysis', ...authorization },
    signal: options.signal,
  });
  if (!res.ok) throw new Error(`GitHub REST API HTTP ${res.status} for ${path.split('?')[0]}`);
  return res.json();
}

/** Acquire a bounded snapshot. The function never posts or mutates GitHub. */
export async function fetchGithubSnapshot(options = {}) {
  const repo = parseRepository(options.repository);
  const sha = parseHeadSha(options.sha);
  const maxRuns = options.maxRuns ?? 100;
  if (!Number.isSafeInteger(maxRuns) || maxRuns < 1 || maxRuns > 200) throw new RangeError('maxRuns must be 1..200');
  const withJobs = options.withJobs === true;
  const request = (path) => requestGithub(path, options);
  const runs = [];
  let total = null;
  for (let page = 1; page <= Math.ceil(maxRuns / 100); page++) {
    const data = await request(`/repos/${repo}/actions/runs?head_sha=${sha}&per_page=100&page=${page}`);
    if (!Array.isArray(data.workflow_runs)) throw new Error('Invalid GitHub workflow_runs response');
    if (page === 1) total = asNonnegativeInteger(data.total_count);
    runs.push(...data.workflow_runs.slice(0, maxRuns - runs.length));
    if (data.workflow_runs.length < 100 || runs.length >= maxRuns) break;
  }
  const jobs_by_run = {};
  if (withJobs) {
    // Keep request concurrency bounded. This reduces latency without flooding the REST API.
    for (let i = 0; i < runs.length; i += 4) {
      const part = runs.slice(i, i + 4);
      const results = await Promise.all(part.map(async run => {
        const data = await request(`/repos/${repo}/actions/runs/${run.id}/jobs?per_page=100&page=1`);
        if (!Array.isArray(data.jobs)) throw new Error(`Invalid jobs response for run ${run.id}`);
        return [String(run.id), { jobs: data.jobs, total_count: data.total_count }];
      }));
      for (const [id, data] of results) jobs_by_run[id] = data;
    }
  }
  return { total_count: total, workflow_runs: runs, jobs_by_run, truncated: total !== null && total > runs.length };
}

export function validateSnapshotShape(snapshot) {
  if (!snapshot || !Array.isArray(snapshot.workflow_runs)) throw new Error('Expected workflow_runs array');
  if (snapshot.workflow_runs.length > 200) throw new Error('Maximum 200 workflow runs per snapshot');
  return true;
}
