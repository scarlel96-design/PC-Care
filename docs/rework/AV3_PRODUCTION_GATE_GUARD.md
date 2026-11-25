# AV3 production-gate regression guard

## Scope

This independent check preserves the WO-29 Phase 1 NO-GO source policy.
It inspects src/SmartPerformanceDoctor.AstraVault/Target/Av3PhaseGate.cs.
It requires six named authorization flags to remain literal false values.
It requires CurrentStatus to remain Av3ProductionStatus.ReadOnlyValidated.
Missing, duplicated, or nonliteral declarations fail the check.

The monitored flags are:

- ProductionWriterEnabled
- JournalWriterEnabled
- MigrationEnabled
- WriterEnableReady
- ExternalReviewCompleted
- ProductionEnableAuthorized

## Run

Prerequisite: Install Node.js 22 or later. Do not install npm packages.

1. Run node --test tests/quality/check-av3-production-gates.test.mjs.
2. Run node scripts/check-av3-production-gates.mjs from the repository root.
3. Require exit code 0 from both commands.

The dedicated GitHub Actions workflow repeats both commands.

## Interpretation and limits

PASS means these seven source declarations matched the expected values.
PASS does not authorize production AV3 writing or prove runtime security.
The check does not inspect compiled binaries, delegated permissions, or file isolation.
It does not replace SecurityLab, Windows CI, or independent review.
Refactoring a monitored field requires a reviewed update to this checker.
Enable production features only after a separate authorization decision.

## Work isolation

This change adds only a checker, tests, documentation, and a CI workflow.
It does not modify AV3 runtime code, main, or the existing WO-29 branch.
Base commit: e638ca51ca0e12082df0d00dffb037a583e097c0.
