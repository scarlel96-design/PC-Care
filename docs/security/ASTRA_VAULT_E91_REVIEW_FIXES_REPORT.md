# PC 케어 보안 금고 — Phase E-9.1 Review Fixes Report (complete)

**Date:** 2026-07-06  
**M-01:** **CLOSED** · **M-02:** **CLOSED**  
**Enable flags:** unchanged **false** (`ExternalReviewCompleted`, `WriterEnableReady`, `ProductionWriterEnabled`, …)

## M-01 — documentation / test evidence

- **Root cause:** E-9 package copied **444** full-suite and **134** filter counts from an earlier snapshot; formal review re-ran `SmartPerformanceDoctor.Tests` Release x64 → **388** pass; filter string change → **114** pass.
- **Fix:** `ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md` + JSON SOT; all current-evidence docs point to latest verified; historical table labeled **historical** only.
- **Enforcement:** `Av3PhaseE91Tests.E91_Documentation_CurrentEvidence_MatchesSourceOfTruth_NoStale444`

## M-02 — commit guard parallel isolation

- **Root cause:** Process-wide static in-flight maps without guaranteed per-root lease release under parallel xUnit.
- **Fix:** `Av3HarnessCommitGuardRegistry` with canonical-root slots + `IAv3CommitGuardLease`; `PurgeRootHarnessState` after harness; pipeline `finally` purge; parallel E91 tests (no serial collection as primary fix).
- **Diagnostic only:** `DiagnosticResetAllHarnessStateForTests` — not used in production paths.

## Guard structure (summary)

| Layer | Behavior |
|-------|----------|
| Canonical root key | `TryNormalizeHarnessRoot` |
| Acquire | One in-flight lease per root; duplicate `transactionId` per root blocked |
| Dispose lease | Clears in-flight only; tx id remains until root purge |
| Purge root | Removes slot (post harness/dry-run) |

## Sign-off posture

- Formal external review: **CONDITIONAL PASS ADDRESSED**
- Production writer: **NOT AUTHORIZED**
- Writer enable readiness: **NO-GO**
- `ExternalReviewCompleted`: **false** (true-candidate only after named sign-off)
- **Next:** Named sign-off / **E-10 Enable Decision Gate**