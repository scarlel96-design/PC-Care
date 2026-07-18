# PC 케어 보안 금고 — External Security Review Checklist (Phase E-9)

**Purpose:** Items a third-party reviewer should verify before any **discussion** of production writer enable.  
**This checklist completion does not set `ExternalReviewCompleted=true` in code** — sign-off is recorded only in `ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md` with named approvers.

**Status:** Package **external-review-ready** · Production writer **NOT AUTHORIZED** · `WriterEnableReady` **NO-GO**  
**Test evidence (latest verified):** `ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md` (E-DOC-SOT) — **455/455** full, **191/191** AV3 filter

## 1. Enable gate flags (must remain false in release)

| # | Check | Evidence |
|---|-------|----------|
| 1.1 | `ProductionWriterEnabled == false` | `Av3PhaseGate.cs`, `Av3PhaseE9Tests` |
| 1.2 | `JournalWriterEnabled == false` | Same |
| 1.3 | `MigrationEnabled == false` | Same |
| 1.4 | `WriterEnableReady == false` | `Av3EnableReadinessChecklist.cs` |
| 1.5 | `ExternalReviewCompleted == false` until formal sign-off | Decision record sign-off table empty |
| 1.6 | `SClassTargetSatisfied == false` | Phase gate + gap report |
| 1.7 | `ProductionAnchorImplemented == false` | Phase gate |
| 1.8 | `XChaCha24Implemented == false` | Phase gate |

## 2. Route isolation (production vs harness vs dry-run)

| # | Check | Evidence |
|---|-------|----------|
| 2.1 | `CommitAsync` / `OpenWriteSessionAsync` blocked on production route | `Av3PhaseE7Tests` |
| 2.2 | Factory `TryCreateProductionRoute` always fails | `Av3PhaseE6Tests`, `Av3PhaseE7Tests` |
| 2.3 | Dry-run only on `av3-e8-` or `av3-harness-` under OS temp | `Av3DryRunScope`, `Av3PhaseE8Tests` |
| 2.4 | Harness roots reject Documents/Desktop/Downloads | `Av3PhaseE71Tests` |
| 2.5 | Relative path escape blocked in durable store | `Av3PhaseE6Tests`, `Av3PhaseE8Tests` |

## 3. Service / UI / import / export (no AV3 writer)

| # | Check | Evidence |
|---|-------|----------|
| 3.1 | `SecureVaultService` — no `SmartPerformanceDoctor.AstraVault.Commit` references | Reflection tests |
| 3.2 | `AstraVaultHostService` — no Commit/DryRun references | `Av3PhaseE9Tests` |
| 3.3 | `SecureVaultViewModel` — no Commit/WriterDesign writer surface | `Av3PhaseE5Tests`, `Av3PhaseE9Tests` |
| 3.4 | No UI path implying AV3 production writer enabled | Docs + gates |

## 4. Telemetry / report non-leak

| # | Check | Evidence |
|---|-------|----------|
| 4.1 | Gate / cancellation / invariant public summaries scanned | `Av3PhaseE7Tests`, `Av3JournalLeakScanner` |
| 4.2 | Dry-run report/manifest/trace — no password/VMK/DEK/paths | `Av3DryRunTelemetryScanner`, `Av3PhaseE8Tests` |
| 4.3 | Journal binary structural vs textual scan separation | `Av3PhaseE61Tests` |

## 5. Invariant and trust chain

| # | Check | Evidence |
|---|-------|----------|
| 5.1 | Uncommitted / cancel / cleanup fail — no `NewGenerationOpen` misuse | E-7/E-8 tests |
| 5.2 | Trusted generation preserved until commit | `ValidateTrustedGenerationPreserved` |
| 5.3 | Post-flush reread + auth before trust | Pipeline + E-6.2 cleanup tests |
| 5.4 | Repair/recovery — classification only, no auto-mutation | `Av3PhaseE71Tests`, `PerformsAutomaticRepair=false` |

## 6. Fault matrix and dry-run (harness only)

| # | Check | Evidence |
|---|-------|----------|
| 6.1 | E-8 dry-run E2E on synthetic fixtures | `Av3PhaseE8Tests` |
| 6.2 | Read-only revalidation after successful dry-run | `Av3DryRunReadOnlyRevalidator` |
| 6.3 | FI: flush, reread, auth, cleanup, cancel | `Av3PhaseE8Tests`, E-6.2 |
| 6.4 | No FI claims on **user** vault paths | Scope docs + dry-run scope |

## 7. Legacy spd-vault and migration

| # | Check | Evidence |
|---|-------|----------|
| 7.1 | AV3 writer/dry-run does not alter spd-vault on-disk format | No migration impl; `MigrationEnabled=false` |
| 7.2 | R9 migration deferred to Phase H | Risk register, checklist |
| 7.3 | No automatic origin deletion default | `Av3DefaultWritePolicy`, invariants |

## 8. S-Class / crypto / anchor (blockers — not claimed done)

| # | Check | Evidence |
|---|-------|----------|
| 8.1 | ChaCha 12-byte nonce labeled BELOW S-CLASS | `ChaCha12ByteNonceBelowSClass=true` |
| 8.2 | XChaCha24 plan documented, **not implemented** | Migration plan doc |
| 8.3 | Anchor model documented, **not implemented** | Anchor model doc |
| 8.4 | Full vault rollback without anchor — limitation disclosed | Anchor model, R10 |

## Reviewer verdict (template)

| Field | Value |
|-------|-------|
| Package reviewed | Y / N |
| P0 / P1 / P2 counts | |
| Recommendation | **NO-GO** / CONDITIONAL GO (hardening only) / GO (enable) — default **NO-GO** |
| `ExternalReviewCompleted` may flip | Only after signed decision record — **not** by checklist alone |

**Forbidden wording in review outcome:** “production-ready”, “S-Class achieved”, “writer authorized” unless decision record explicitly GO **and** all gates flipped per policy (out of scope for E-9).