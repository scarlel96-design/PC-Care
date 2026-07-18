# PC 케어 보안 금고 — Data Loss Risk Register (AV3 / Phase E-9)

**Scope:** AV3 writer/journal path (design); legacy spd-vault unchanged  
**S-Class:** NOT YET SATISFIED  
**E-14 (2026-07-08):** Disk durability review package **COMPLETE** (candidate only); harness `av3-e14-` **CLOSED**; `ActualDiskDurabilityReviewed=false`; production disk durability **OPEN** until E-14.1.

**E-13.1 (2026-07-06):** B-1 anchor **PARTIAL / SIGNED CANDIDATE** — harness+stub **SIGNED**; live external witness **없음**; production R10/full-vault rollback **PARTIAL**.  
**E-11.1 (2026-07-06):** B-1 anchor harness signed candidate baseline.  
**E-12.1 (2026-07-06):** B-2 XChaCha24 **APPROVED CANDIDATE** — crypto sign-off; `XChaCha24Implemented=false`; R12 residual **PARTIAL CLOSED** (code flag still false).

| ID | Risk | Severity | Phase E-0 mitigation (design) | Residual |
|----|------|----------|-------------------------------|----------|
| R1 | Partial write exposes corrupt new generation as current | **High** | Post-flush reread + AEAD; old gen preserved until `Committed` | **E-4 CLOSED (harness)** — `Av3TornWriteSimulator` / `Av3AtomicWriteValidator`; production writer still off |
| R2 | Single header copy loss | **High** | 3-copy policy; `RedundancyDegraded` | **E-4 CLOSED (classification)** — `Av3HeaderRepairClassifier`; auto-repair **not implemented** |
| R3 | Conflicting header copies same generation | **High** | Consensus reject (existing validator) | **E-4 CLOSED (classification)** — `Av3HeaderConflictEvidence` |
| R4 | Journal trusted without activation | **High** | Root of trust = activation; journal hints only | Documented |
| R5 | Disk full mid-commit | **Medium** | Abort; previous gen open | User message |
| R6 | External drive removal | **Medium** | Abort; no silent truncate | UI gate |
| R7 | Temp decrypt path leak | **Medium** | Secure temp policy (writer gate §5) | Audit E+ |
| R8 | Accidental original delete on import | **Medium** | No auto-delete default | Legacy UI unchanged |
| R9 | Migration destroys spd-vault | **High** | `MigrationEnabled=false`; Phase H separate | **Phase H only** — AV3 E-4 writer harness does not touch spd-vault or migration |
| R10 | Rollback attack (downgrade generation) | **High** | Rollback suspected rules + E-11 harness anchor witness | **E-4 CLOSED (local harness)**; **E-11 PARTIAL** — harness monotonic anchor; **full vault rewind** still needs **trusted monotonic anchor** |
| R11 | Cleartext filename in journal | **High** | Journal digest-only design | **E-6.1 CLOSED (harness)** — structural scan + textual surfaces; RNG false-positive removed |
| R12 | ChaCha 12-byte nonce suite below S-Class | **Medium** | E-12/E-12.1 crypto package | **E-12.1 APPROVED CANDIDATE** — TARGET AEAD signed; `XChaCha24Implemented=false`; S-Class aggregate still open |

## Phase E-5 note

Production writer **design** locked; enable **NO-GO**. Anchor model documented (`ASTRA_VAULT_ANCHOR_MODEL.md`); production anchor **not implemented**.

## Phase E-6 note

Disabled writer implementation (`Commit/*`) exercises R1/R2/R3/R11 paths on **isolated `av3-e*` harness only**. Residual **High:** no production crash-safe writer on user vaults; **Medium:** metadata graph materialization not implemented; **R12** XChaCha24 not implemented. spd-vault on-disk **unchanged**.

## Phase E-6.1 note

R11 journal confidentiality harness no longer applies forbidden-token UTF-8 scan to raw digest bytes. Cleartext detection uses JNAL structural validation + trailing appendix textual scan. `JournalLeakScannerDeterministic` / `JournalBinaryScanSeparated` = true. Production writer **NOT AUTHORIZED**.

## Phase E-6.2 note

Cleanup failure FI separates `PostAuthDataTrusted` from `Committed` and maps `Av3CommitCleanupPosture` without promoting cleanup faults to `NewGenerationOpen`. External review E-6/E-6.1: **CONDITIONAL GO**, Critical/High none; `ExternalReviewCompleted` still **false**. E-7: hardening only.

## Phase E-7 note

Pre-enable hardening: invariants, negative production matrix, cancellation/concurrency guard. Recovery/repair remain **classification/plan only** — no automatic file repair. Writer enable **NO-GO**. S-Class blockers: production anchor, XChaCha24, external review sign-off.

## Phase E-8 note (harness / dry-run reduced — production open)

**Reduced (harness/dry-run only):** End-to-end commit posture on synthetic fixtures; read-only revalidation; telemetry non-leak; extended FI matrix. **Does not** close production writer on user vaults.

**Still open (production):** Real media durability, service/UI routes, migration, anchor-backed rollback, XChaCha24. spd-vault on-disk **unchanged**. `ExternalReviewCompleted=false`.

## Phase E-9 note

External review package/checklist/evidence index refreshed for third-party review. **Harness/dry-run CLOSED ≠ production CLOSED.** R10 full-vault rollback without anchor — **not** claimed fully solved. R12 XChaCha blocker unchanged.

## Phase E-7.1 note

E-7 engineering review fixes: trusted generation preserved until commit; cleanup failure cannot imply `NewGenerationOpen`; harness roots restricted to OS temp + approved prefixes (not user Documents/Desktop/Downloads). Repair classifier tests assert **no storage mutation**. `ExternalReviewCompleted` still **false**. E-8 = limited dry-run / RC hardening only.

## Review cadence

- Update on production route enable review (post E-6)  
- Cross-check with `ASTRA_VAULT_GAP_REPORT.md`

## Data loss acceptance (explicit non-goals)

- Writer enable **not** approved until R1–R4, R9–R11 have automated FI evidence.  
- No user production vaults in FI harness.