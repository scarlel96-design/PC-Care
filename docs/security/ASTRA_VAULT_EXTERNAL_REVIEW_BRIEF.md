# PC 케어 보안 금고 — External Security Review Brief (Phase E-9)

**Product:** PC 케어 보안 금고 (internal AV3 / `SmartPerformanceDoctor.AstraVault`)  
**Date:** 2026-07-06  
**Package:** **external-review-ready** — not production authorization

## Review package vs review completed

| Flag / field | Meaning |
|--------------|---------|
| `ExternalReviewPackageReady` | Brief, questionnaire, and design bundle **prepared for reviewers** |
| `ExternalReviewCompleted` | **false** — third-party sign-off **not done** |
| `WriterEnableReady` | **false** until `ExternalReviewCompleted` and other blockers clear |

**Do not** treat “package READY” or “ready for external review” as approval to enable `ProductionWriterEnabled`.

## Executive summary

AV3 remains **NOT PRODUCTION**. Legacy **spd-vault** stays **BELOW S-CLASS** with **no on-disk changes** from AV3 writer/dry-run. E-6–E-8: disabled writer, hardening, limited dry-run (synthetic/isolated temp). **Production writing disabled** (`ProductionWriterEnabled = false`).

## Current phase status

| Phase | Status | Enable impact |
|-------|--------|---------------|
| E-5 | Design + package | None |
| E-6 / E-6.1 / E-6.2 | Disabled writer + R11 + fixes | None |
| E-7 / E-7.1 | Pre-enable hardening | None |
| E-8 | Limited dry-run | None |
| **E-9** | External sign-off prep | Package refreshed; **not** sign-off |

## Production writer disabled (evidence)

- `Av3PhaseGate` enable flags **false** (tests)
- `Commit/*` production routes fail-closed; `DryRun/*` scope-limited
- No `SecureVaultService` / Host / ViewModel Commit or DryRun wiring (reflection tests)

## Closed risks (harness)

- R1 partial/torn write — classification harness
- R2/R3 header degradation/conflict — repair classifier
- R10 local rollback signals — detector + documented full-vault limitation
- R11 journal cleartext — scanner + digest-only v1 policy

## Open risks / S-Class blockers

- R9 migration — Phase H
- Production durable writer on real user media — not implemented
- XChaCha24 TARGET crypto — plan only
- External/trusted anchor — model only
- External security review — **not completed**
- Encrypted metadata graph writer — deferred

## Test summary (latest verified — E-9.1)

- **E-TEST-SOT:** full Release x64 — **545/545 PASS** (E-12 SOT; historical **517/501/477/455** in SOT)
- **E-TEST-AV3-FILTER:** **281/281 PASS** (see `ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md`)
- **Historical:** E-9 prep **444/444** and **134/134** — phase history only, not enable evidence
- Golden vectors LOCKED (`test-vectors/av3/`)

## Fault injection summary

See `ASTRA_VAULT_FAULT_INJECTION_PLAN.md`. Actual kill + simulated torn writes; no production writer FI on user paths.

## Rollback limitation

Whole-vault restore without anchor → **complete detection impossible** (`ASTRA_VAULT_ANCHOR_MODEL.md`).

## Journal confidentiality

Digest-only journal v1; no paths/passwords on disk; leak scanners in CI.

## Anchor plan

Design options documented; **not implemented**.

## XChaCha plan

Migration plan documented; CURRENT 12-byte nonce **BELOW S-CLASS**.

## No-go conditions

- Any finding that post-flush auth can be bypassed
- Cleartext sensitive fields in journal or logs
- Writer enable without review sign-off
- Silent spd-vault migration or origin deletion

## Artifacts for reviewers

`ASTRA_VAULT_PRODUCTION_WRITER_REVIEW_PACKAGE.md`, `ASTRA_VAULT_EXTERNAL_REVIEW_CHECKLIST.md`, `ASTRA_VAULT_EXTERNAL_REVIEW_EVIDENCE_INDEX.md`, questionnaire, decision record (NO-GO; sign-off empty).

## Forbidden interpretations

“Production-ready”, “S-Class achieved”, or writer authorized from E-8 dry-run or E-9 package READY alone.