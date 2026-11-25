import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const PRODUCTION_GATES = Object.freeze([
  'ProductionWriterEnabled',
  'JournalWriterEnabled',
  'MigrationEnabled',
  'WriterEnableReady',
  'ExternalReviewCompleted',
  'ProductionEnableAuthorized',
]);

const SOURCE_FILE = fileURLToPath(new URL(
  '../src/SmartPerformanceDoctor.AstraVault/Target/Av3PhaseGate.cs', import.meta.url,
));

// Reject an unknown declaration. Only a literal false satisfies this policy.
export function auditAv3Source(source) {
  if (typeof source !== 'string') throw new TypeError('Expected source text');
  const errors = [];

  for (const name of PRODUCTION_GATES) {
    const declaration = new RegExp(
      `^[ \\t]*public[ \\t]+(?:static[ \\t]+readonly|const)[ \\t]+bool[ \\t]+${name}[ \\t]*=[ \\t]*([^;\\r\\n]+)[ \\t]*;`,
      'gm',
    );
    const matches = [...source.matchAll(declaration)];
    if (matches.length !== 1) {
      errors.push(`${name}: expected exactly one literal declaration; found ${matches.length}`);
      continue;
    }
    if (matches[0][1].trim() !== 'false') {
      errors.push(`${name}: expected false; found ${matches[0][1].trim()}`);
    }
  }

  const status = [...source.matchAll(
    /^[ \t]*public[ \t]+const[ \t]+Av3ProductionStatus[ \t]+CurrentStatus[ \t]*=[ \t]*Av3ProductionStatus\.([^;\r\n]+)[ \t]*;/gm,
  )];
  if (status.length !== 1 || status[0][1].trim() !== 'ReadOnlyValidated') {
    errors.push('CurrentStatus: expected one ReadOnlyValidated declaration');
  }

  return Object.freeze({ ok: errors.length === 0, checked: PRODUCTION_GATES.length + 1, errors });
}

// Execute only when invoked as a command, not when imported by unit tests.
if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  try {
    const result = auditAv3Source(readFileSync(SOURCE_FILE, 'utf8'));
    for (const error of result.errors) console.error(`AV3_GATE_FAIL: ${error}`);
    if (!result.ok) process.exitCode = 1;
    else console.log(`AV3_GATE_PASS: ${result.checked} release-gate contracts verified`);
  } catch (error) {
    console.error(`AV3_GATE_ERROR: ${error.message}`);
    process.exitCode = 1;
  }
}
