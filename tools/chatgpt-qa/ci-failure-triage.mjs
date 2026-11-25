/** Read-only CI failure diagnostics. Do not treat log evidence as build reproduction. */

function asPositiveInt(value, label) {
  if (!Number.isSafeInteger(value) || value <= 0) throw new TypeError(`${label} must be a positive integer`);
  return value;
}

function normalizePath(path) {
  const slash = path.replaceAll('\\', '/');
  const at = slash.lastIndexOf('/src/');
  if (at !== -1) return slash.slice(at + 1);
  const alt = slash.lastIndexOf('/tests/');
  if (alt !== -1) return slash.slice(alt + 1);
  return slash.split('/').at(-1);
}

export function parseJobLog(log, { runId = null, jobId = null } = {}) {
  if (typeof log !== 'string') throw new TypeError('log must be a string');
  const locations = new Map();
  const codes = Object.create(null);
  const sdkVersions = new Set();
  let emittedErrorLines = 0;
  let unlocatedErrorLines = 0;
  for (const raw of log.split(/\r?\n/)) {
    const line = raw.replace(/\x1b\[[0-9;]*m/g, '');
    const sdk = line.match(/dotnet-install: (?:\.NET Core SDK with version|'?Installed version is'?)\s*['"]?((?:\d+\.){2}\d+)/i);
    if (sdk) sdkVersions.add(sdk[1]);
    const code = line.match(/\berror\s+([A-Za-z][A-Za-z0-9]*\d+):/i);
    if (!code) continue;
    emittedErrorLines++;
    codes[code[1]] = (codes[code[1]] || 0) + 1;
    const source = line.match(/((?:[A-Za-z]:[\\/]|\/)[^()\r\n]+?\.(?:cs|xaml))\((\d+),(\d+)\):\s*error\s+([A-Za-z][A-Za-z0-9]*\d+):/i);
    if (!source) { unlocatedErrorLines++; continue; }
    const file = normalizePath(source[1]);
    const key = `${source[4]}|${file}:${source[2]}:${source[3]}`;
    const existing = locations.get(key) ?? { code: source[4], file, line: Number(source[2]), column: Number(source[3]), occurrences: 0 };
    existing.occurrences++;
    locations.set(key, existing);
  }
  const diagnostics = [...locations.values()].sort((a, b) =>
    a.code.localeCompare(b.code) || a.file.localeCompare(b.file) || a.line - b.line || a.column - b.column);
  return {
    runId, jobId, sdkVersions: [...sdkVersions].sort(), emittedErrorLines,
    unlocatedErrorLines, uniqueLocations: diagnostics.length,
    uniqueFiles: new Set(diagnostics.map(d => d.file)).size,
    codes: { ...codes }, diagnostics,
  };
}

export function summarizeFailureEvidence(input) {
  if (!input || !Array.isArray(input.runs) || !input.runs.length) {
    throw new TypeError('Expected nonempty runs array');
  }
  const seen = new Set();
  const grouped = new Map();
  const sdkByOutcome = { failed: new Set(), successful: new Set() };
  const problems = [];
  let failed = 0;
  let complete = 0;
  let totalEmissions = 0;
  const locations = new Set();
  for (const run of input.runs) {
    const id = asPositiveInt(run.runId, 'runId');
    if (seen.has(id)) { problems.push(`Duplicate run ${id}`); continue; }
    seen.add(id);
    if (run.conclusion === 'success') {
      if (typeof run.sdk === 'string') sdkByOutcome.successful.add(run.sdk);
      continue;
    }
    if (run.conclusion !== 'failure') { problems.push(`Run ${id} is not a completed failure`); continue; }
    failed++;
    if (typeof run.sdk === 'string') sdkByOutcome.failed.add(run.sdk);
    if (!Number.isSafeInteger(run.jobId) || run.jobId <= 0 || !run.logSummary || typeof run.logSummary !== 'object') {
      problems.push(`Run ${id} lacks verified job/log evidence`);
      continue;
    }
    complete++;
    const s = run.logSummary;
    if (!Number.isSafeInteger(s.emittedErrorLines) || s.emittedErrorLines < 0 || !Number.isSafeInteger(s.uniqueLocations) || s.uniqueLocations < 0 || !s.codes || typeof s.codes !== 'object') {
      problems.push(`Run ${id} has invalid log summary`);
      continue;
    }
    totalEmissions += s.emittedErrorLines;
    let sumOfCodeLines = 0;
    for (const [code, count] of Object.entries(s.codes)) {
      if (!/^[A-Za-z][A-Za-z0-9]*\d+$/.test(code) || !Number.isSafeInteger(count) || count < 0) {
        problems.push(`Run ${id} invalid diagnostic code/count`); continue;
      }
      sumOfCodeLines += count;
      const entry = grouped.get(code) ?? { code, affectedRuns: 0, emittedLines: 0 };
      entry.affectedRuns++;
      entry.emittedLines += count;
      grouped.set(code, entry);
    }
    if (sumOfCodeLines !== s.emittedErrorLines || s.uniqueLocations > s.emittedErrorLines) {
      problems.push(`Run ${id} error counters disagree`);
    }
    if (Array.isArray(s.diagnostics)) {
      for (const d of s.diagnostics) if (d?.file && Number.isSafeInteger(d.line) && d.code) locations.add(`${d.code}|${d.file}:${d.line}:${d.column}`);
    }
  }
  if (Number.isSafeInteger(input.expectedFailures) && failed !== input.expectedFailures) {
    problems.push(`Expected ${input.expectedFailures} failures; observed ${failed}`);
  }
  if (complete !== failed) problems.push(`Only ${complete}/${failed} failed runs have log summaries`);
  const groups = [...grouped.values()].sort((a, b) => b.affectedRuns - a.affectedRuns || a.code.localeCompare(b.code));
  const sameFailureCode = groups.filter(g => g.affectedRuns === failed).map(g => g.code);
  const failedSdks = [...sdkByOutcome.failed].sort();
  const passedSdks = [...sdkByOutcome.successful].sort();
  return {
    status: problems.length ? 'PARTIAL' : 'COMPLETE',
    limitations: problems,
    runsSupplied: seen.size, failedRuns: failed, failedRunsWithEvidence: complete,
    emittedErrorLinesAcrossRuns: totalEmissions,
    commonToAllFailures: sameFailureCode,
    codeGroups: groups,
    uniqueDiagnosticLocationsAcrossAvailableDetails: locations.size || null,
    sdkComparison: {
      failed: failedSdks, successful: passedSdks,
      versionsDiffer: failedSdks.length && passedSdks.length ? failedSdks.some(v => !passedSdks.includes(v)) : null,
      causeVerified: false,
      explanation: 'An SDK version difference is correlation. Reproduce under controlled SDK versions before assigning causality.',
    },
  };
}
