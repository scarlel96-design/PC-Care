import test from 'node:test';
import assert from 'node:assert/strict';
import { auditAv3Source, PRODUCTION_GATES } from '../../scripts/check-av3-production-gates.mjs';

const safeSource = () => `public static class Av3PhaseGate
{
    public const Av3ProductionStatus CurrentStatus = Av3ProductionStatus.ReadOnlyValidated;
${PRODUCTION_GATES.map(name => `    public static readonly bool ${name} = false;`).join('\n')}
}`;

test('accepts one explicit false declaration for all production gates', () => {
  const result = auditAv3Source(safeSource());
  assert.equal(result.ok, true, result.errors.join('; '));
  assert.equal(result.checked, 7);
});

for (const name of PRODUCTION_GATES) {
  test(`rejects ${name} when enabled`, () => {
    const text = safeSource().replace(`${name} = false;`, `${name} = true;`);
    const result = auditAv3Source(text);
    assert.equal(result.ok, false);
    assert.ok(result.errors.some(error => error.startsWith(`${name}:`)));
  });
}

test('rejects missing gates instead of silently passing', () => {
  const text = safeSource().replace(/.*ProductionWriterEnabled = false;\n/, '');
  assert.equal(auditAv3Source(text).ok, false);
});

test('rejects duplicate declarations', () => {
  const text = `${safeSource()}\npublic static readonly bool MigrationEnabled = false;`;
  assert.ok(auditAv3Source(text).errors.some(error => error.startsWith('MigrationEnabled:')));
});

test('rejects nonliteral values', () => {
  const text = safeSource().replace('MigrationEnabled = false;', 'MigrationEnabled = EnvironmentFlag;');
  assert.equal(auditAv3Source(text).ok, false);
});

test('rejects an unauthorized production status', () => {
  const text = safeSource().replace('Av3ProductionStatus.ReadOnlyValidated;', 'Av3ProductionStatus.ProductionAuthorized;');
  assert.ok(auditAv3Source(text).errors.some(error => error.startsWith('CurrentStatus:')));
});

test('does not count a commented out single-line declaration', () => {
  const text = safeSource().replace('public static readonly bool MigrationEnabled', '// public static readonly bool MigrationEnabled');
  assert.equal(auditAv3Source(text).ok, false);
});

test('rejects wrong source types', () => {
  assert.throws(() => auditAv3Source(null), TypeError);
});
