/** Read-only .NET SDK selection audit. This tool does not establish build causation. */
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { resolve, join } from 'node:path';

const EXACT = /^\d+\.\d+\.\d+$/;
const FLOATING = /(?:^|[.])(?:x|X|\*|latest)(?:$|[.])|\$\{\{|\$\(|\$\{/;
const RISK = { NOT_APPLICABLE: 0, GUARDED_EXACT: 0, EXACT_NO_GUARD: 1, FLOATING: 2, MIXED: 3, UNKNOWN: 3, CONFLICT: 4 };

function stepBlocks(source) {
  const lines = source.split(/\r?\n/);
  const blocks = [];
  let block = null;
  for (const line of lines) {
    if (/^\s{6}-\s+/.test(line)) {
      if (block) blocks.push(block);
      block = [line];
    } else if (block && (/^\s{4}\S/.test(line) || /^\s{2}\S/.test(line) || /^\S/.test(line))) {
      blocks.push(block); block = null;
    } else if (block) block.push(line);
  }
  if (block) blocks.push(block);
  return blocks.map(lines => lines.join('\n'));
}

function getSetupSelectors(source) {
  const selectors = [];
  for (const step of stepBlocks(source)) {
    if (!/^\s{6}-\s+uses:\s*['"]?actions\/setup-dotnet@[^\s'"#]+/m.test(step)
      && !/^\s{8}uses:\s*['"]?actions\/setup-dotnet@[^\s'"#]+/m.test(step)) continue;
    const matches = [...step.matchAll(/^\s{10}dotnet-version:\s*['"]?([^'"#\r\n]+?)['"]?\s*(?:#.*)?$/gm)];
    if (!matches.length) { selectors.push({ value: null, kind: 'UNKNOWN' }); continue; }
    for (const match of matches) {
      for (const raw of match[1].trim().split(/\s+/)) {
        const value = raw.trim();
        if (!value) continue;
        const kind = EXACT.test(value) ? 'EXACT' : FLOATING.test(value) || !/^\d+\.\d+/.test(value) ? 'FLOATING' : 'UNKNOWN';
        selectors.push({ value, kind });
      }
    }
  }
  return selectors;
}

export function inspectWorkflow(name, source) {
  if (typeof name !== 'string' || typeof source !== 'string') throw new TypeError('Expected workflow filename and text');
  const selectors = getSetupSelectors(source);
  const performsBuild = /\bdotnet\s+(?:build|test|restore|publish|pack)\b/i.test(source);
  const pinLines = source.split(/\r?\n/).filter(line => /Set-Content\s+(?:-Path\s+)?['"]?global\.json/i.test(line));
  const pins = pinLines.map(line => {
    const version = line.match(/"sdk"\s*:\s*\{\s*"version"\s*:\s*"([\d.]+)"/i)?.[1] ?? null;
    const disabled = /"rollForward"\s*:\s*"disable"/i.test(line);
    return { version, disabled };
  });
  const assertions = [...source.matchAll(/\bdotnet\s+--version\)?\s*-ne\s*['"](\d+\.\d+\.\d+)['"]/gi)].map(x => x[1]);
  let classification;
  if (!selectors.length) classification = performsBuild ? 'UNKNOWN' : 'NOT_APPLICABLE';
  else if (selectors.some(x => x.kind === 'UNKNOWN')) classification = 'UNKNOWN';
  else if (selectors.some(x => x.kind === 'FLOATING') && selectors.some(x => x.kind === 'EXACT')) classification = 'MIXED';
  else if (selectors.every(x => x.kind === 'FLOATING')) classification = 'FLOATING';
  else {
    const versions = [...new Set(selectors.map(x => x.value))];
    const consistent = versions.length === 1 && pins.length === 1 && pins[0].disabled && pins[0].version === versions[0] && assertions.includes(versions[0]);
    classification = consistent ? 'GUARDED_EXACT' : 'EXACT_NO_GUARD';
  }
  const pinnedVersions = pins.map(x => x.version).filter(Boolean);
  if (selectors.length && pinnedVersions.length && selectors.some(x => x.kind === 'EXACT' && !pinnedVersions.includes(x.value))) classification = 'CONFLICT';
  if (pins.length > 1 && selectors.length) classification = 'UNKNOWN';
  return { file: name, classification, setupSelectors: selectors, scriptGlobalJson: pins, versionAssertions: assertions,
    risk: RISK[classification], performsBuild,
    caveat: 'Static workflow configuration only; not proof of SDK selected by dotnet or any build result.' };
}

export function inspectRunLog(log) {
  if (typeof log !== 'string') throw new TypeError('Log must be text');
  const configured = [...log.matchAll(/\bdotnet-version:\s*['"]?([^'"\s\r\n]+)/g)].map(x => x[1]);
  const installed = [...log.matchAll(/dotnet-install:\s*(?:Installed version is\s*['"]?|\.NET Core SDK with version\s*['"])(\d+\.\d+\.\d+)/gi)].map(x => x[1]);
  const sdk = [...new Set(installed)];
  const failures = [...new Set([...log.matchAll(/\berror\s+([a-z]+\d+)\s*:/gi)].map(x => x[1]))].sort();
  return { configured: [...new Set(configured)], installed: sdk, compilerErrorCodes: failures,
    selectedSdkProven: false,
    note: 'setup-dotnet installation evidence is not the same as a recorded dotnet --version invocation.' };
}

export function summarizeSdkComparisons(observations) {
  if (!Array.isArray(observations) || !observations.length) throw new TypeError('Expected observations');
  const ids = new Set(), problems = [], success = new Set(), failure = new Set();
  for (const r of observations) {
    if (!Number.isSafeInteger(r.runId) || r.runId <= 0 || ids.has(r.runId)) { problems.push('Missing or duplicate run id'); continue; }
    ids.add(r.runId);
    if (!['success','failure'].includes(r.conclusion) || !EXACT.test(r.installedSdk ?? '')) {
      problems.push(`Unverified outcome or installed SDK for ${r.runId}`); continue;
    }
    (r.conclusion === 'success' ? success : failure).add(r.installedSdk);
  }
  return { status: problems.length ? 'PARTIAL' : 'COMPLETE', analyzedRuns: ids.size,
    successfulInstalledSdks: [...success].sort(), failedInstalledSdks: [...failure].sort(),
    sdkVersionsDiffer: Boolean(success.size && failure.size && [...failure].some(x => !success.has(x))),
    causeVerified: false, problems,
    note: 'The sample establishes correlation only. Same-source clean A/B builds remain required.' };
}

export function auditDirectory(root, baseline = null) {
  const directory = join(resolve(root), '.github', 'workflows');
  if (!existsSync(directory)) throw new Error('Workflow directory missing');
  const files = readdirSync(directory).filter(x => /\.ya?ml$/.test(x)).sort();
  const entries = files.map(file => inspectWorkflow(file, readFileSync(join(directory, file), 'utf8')));
  const counts = {};
  for (const e of entries) counts[e.classification] = (counts[e.classification] || 0) + 1;
  const regressions = [];
  if (baseline) {
    if (!baseline.workflows || typeof baseline.workflows !== 'object') throw new TypeError('Baseline must have workflows map');
    for (const [file, previous] of Object.entries(baseline.workflows)) {
      const now = entries.find(x => x.file === file);
      if (!now) { regressions.push({ file, reason: 'Baseline workflow missing' }); continue; }
      if (!(previous.classification in RISK)) { regressions.push({ file, reason: 'Unknown baseline classification' }); continue; }
      if (now.risk > RISK[previous.classification]) regressions.push({ file, before: previous.classification, after: now.classification, reason: 'Risk increased' });
      if (previous.classification === 'GUARDED_EXACT' && now.classification === 'GUARDED_EXACT' &&
          JSON.stringify(previous.setupSelectors ?? []) !== JSON.stringify(now.setupSelectors))
        regressions.push({ file, reason: 'Guarded SDK selector changed without qualification' });
    }
    for (const item of entries) if (!(item.file in baseline.workflows) && item.risk > 0)
      regressions.push({ file: item.file, reason: 'New workflow requires SDK review', after: item.classification });
  }
  return { status: regressions.length ? 'BLOCKED' : (counts.FLOATING || counts.UNKNOWN || counts.MIXED || counts.EXACT_NO_GUARD ? 'ADVISORY' : 'PASS'),
    workflowCount: files.length, counts, regressions, entries, baselineCompared: Boolean(baseline),
    warning: 'ADVISORY does not mean build failure or product completion.' };
}
