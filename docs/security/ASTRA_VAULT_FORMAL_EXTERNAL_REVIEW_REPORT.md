# PC 케어 보안 금고 — Formal External Security Review Report (Phase E-9)

**Review type:** Third-party-style formal review (documentation + code + test evidence)  
**Review date:** 2026-07-06  
**Scope:** AV3 writer enable **pre-authorization** — **not** production enable, **not** code flag changes  
**Product (user-facing):** PC 케어 보안 금고 · Internal: `SmartPerformanceDoctor.AstraVault` (AV3)  
**Legacy:** spd-vault **BELOW S-CLASS** — on-disk unchanged by this review

## 1. Review scope performed

| Area | Coverage |
|------|----------|
| E-9 external review package | Checklist, evidence index, review package, brief, decision record, risk register |
| Phase / gate docs | `ASTRA_VAULT_PHASE_STATUS.md`, `ASTRA_VAULT_HARDENING_PLAN.md`, `ASTRA_VAULT_GAP_REPORT.md`, writer enable checklist/gate |
| Gates | `Av3PhaseGate.cs`, `Av3EnableReadinessChecklist.cs` |
| Route / scope isolation | `Av3WriterAccessGate.cs`, `Av3DryRunScope.cs`, `Av3WriterHarnessFactory.cs`, `Av3CommitOrchestrator.cs` |
| Commit pipeline / invariants | `Av3CommitHeaderCommitter.cs`, `Av3CommitPipelineRunner.cs`, `Av3WriterInvariantValidator.cs`, `Av3CommitRecoveryManager.cs` |
| Dry-run | `DryRun/*`, synthetic fixtures, telemetry scanner |
| E-6–E-9 tests | `Av3PhaseE6Tests`–`Av3PhaseE9Tests`, gate/fault/cancellation/invariant/dry-run filters |
| Service/UI isolation | Reflection tests (`Av3PhaseE9Tests`, E-5/E-6/E-7/E-8) |
| Build verification | `dotnet format --verify-no-changes`, Release x64 build + full/filtered test (this review run) |

**Out of scope (explicit):** Production writer enable, migration implementation, anchor/XChaCha24 implementation, spd-vault mutation, automatic repair.

## 2. Documents and artifacts reviewed

1. `docs/security/ASTRA_VAULT_EXTERNAL_REVIEW_CHECKLIST.md`
2. `docs/security/ASTRA_VAULT_EXTERNAL_REVIEW_EVIDENCE_INDEX.md`
3. `docs/security/ASTRA_VAULT_PRODUCTION_WRITER_REVIEW_PACKAGE.md`
4. `docs/security/ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md`
5. `docs/security/ASTRA_VAULT_EXTERNAL_REVIEW_BRIEF.md`
6. `docs/security/ASTRA_VAULT_DATA_LOSS_RISK_REGISTER.md`
7. `docs/security/ASTRA_VAULT_PHASE_STATUS.md`
8. `docs/security/ASTRA_VAULT_HARDENING_PLAN.md`
9. `docs/security/ASTRA_VAULT_GAP_REPORT.md`
10. `docs/security/ASTRA_VAULT_WRITER_ENABLE_CHECKLIST.md`
11. `docs/security/ASTRA_VAULT_WRITER_GATE.md`
12. `src/SmartPerformanceDoctor.AstraVault/Target/Av3PhaseGate.cs`
13. `tests/SmartPerformanceDoctor.Tests/Av3PhaseE9Tests.cs`
14. E-6–E-8 writer / dry-run / invariant / fault / cancellation code paths referenced in evidence index

## 3. Mandatory review areas — summary

| # | Area | Result | Notes |
|---|------|--------|-------|
| 1 | Gate correctness | **PASS** | All enable flags `false` in code, checklist, invariants |
| 2 | Route isolation | **PASS** | Production `DenyProductionCreate`; factory fails; orchestrator blocked |
| 3 | Dry-run isolation | **PASS** | OS temp + `av3-e8-` / `av3-harness-`; user workspace rejected |
| 4 | Commit pipeline safety | **PASS** (harness) | 3-copy, reread/auth invariants; cleanup/cancel classified; production route N/A |
| 5 | Invariant correctness | **PASS** | Trusted gen, gates closed, no cleanup-fail `NewGenerationOpen` |
| 6 | Confidentiality / non-leak | **PASS** | `av3_*` public errors; journal digest-only; scanners separated |
| 7 | Recovery / repair posture | **PASS** | `PerformsAutomaticRepair=false`; classification only |
| 8 | Data loss risk | **PASS** (disclosure) | Harness ≠ production; R10/R12 blockers documented |
| 9 | Crypto blocker | **PASS** (posture) | ChaCha12 BELOW S-CLASS; XChaCha24/anchor not implemented |
| 10 | Legacy safety | **PASS** | No App Commit/DryRun; `MigrationEnabled=false` |

## 4. Findings

### Critical — 0

No finding that would allow production writer execution, user-vault dry-run, or enable-flag bypass under current code paths.

### High — 0

No finding of service/UI/import-export AV3 writer wiring, legacy spd-vault on-disk mutation by AV3 writer/dry-run, or automatic repair enabling storage mutation.

### Medium — 2

| ID | Finding | Evidence ID | Repro / artifact | False positive? | Production enable impact |
|----|---------|-------------|------------------|-----------------|--------------------------|
| **M-01** | External review package and evidence index state **444/444** full-suite passes; authoritative re-run on 2026-07-06 reports **388/388 PASS** (0 failed). Count drift weakens audit traceability; gates/tests themselves passed. | E-DOC-02, E-DOC-03, E-TEST-GATE | `dotnet test -c Release -p:Platform=x64` (this review) | No | **None** for current disabled posture; **documentation** must be aligned before treating evidence index as authoritative for enable gate |
| **M-02** | `Av3WriterCommitGuard` uses **process-wide static** in-flight/transaction maps (`ConcurrentDictionary` + `ThreadLocal` reentrancy). Parallel xUnit tests sharing vault roots can theoretically interfere (historical flake reports in engineering sessions). Mitigations exist (`ClearVaultHarnessState`, per-test GUID roots) but collection is not globally serialized. | E-FI-02, E-TEST-FI | `Av3WriterCommitGuard.cs`; `Av3PhaseE7Tests` cancel/concurrent/duplicate tx | No (CI reliability) | **None** while `ProductionWriterEnabled=false`; recommend E-9.1 hardening for **evidence stability** before enable decision |

### Low — 1

| ID | Finding | Evidence ID | Repro | False positive? | Production enable impact |
|----|---------|-------------|-------|-----------------|--------------------------|
| **L-01** | General harness accepts broader temp prefixes (`av3-e6-`, `av3-e7-`, …) than dry-run (`av3-e8-` / `av3-harness-` only). Intentional layering; dry-run stricter. Reviewer must not conflate “harness closed” with “dry-run closed” when reading phase tables. | E-ROOT-01, E-ROOT-02 | `Av3PhaseE9Tests.E9_DryRunScope_RequiresE8OrHarnessPrefix` | Partially (by design) | **None** if dry-run entry points remain scope-gated |

### Info — 3

| ID | Finding | Evidence ID | Notes |
|----|---------|-------------|-------|
| **I-01** | Decision record sign-off table remains empty (named approvers pending). | E-DOC-01 | Expected for E-9; formal **approved** sign-off not recorded |
| **I-02** | `ActualDiskDurabilityReviewed=false`, `ReleaseSecurityReviewCompleted=false` in `Av3EnableReadinessChecklist` — explicit production blockers. | E-GATE-02 | Not a defect; required NO-GO posture |
| **I-03** | Prior engineering reviews (E-6/E-7) reported Critical/High **none**; this formal review **confirms** that for current disabled-writer scope. | E-DOC-01 | Does not substitute for named external sign-off |

## 5. Production writer authorization (reviewer judgment)

| Question | Judgment |
|----------|----------|
| May `ProductionWriterEnabled` be set true based on this review? | **NO** |
| May `WriterEnableReady` be set true? | **NO** |
| Is AV3 writer implementation safe to **discuss** for a future enable gate? | **YES**, subject to remaining blockers and E-9.1 doc/CI hygiene |
| Production Writer Authorization | **NOT AUTHORIZED** |

## 6. Writer enable readiness

| Criterion | Status |
|-----------|--------|
| Formal external review (this report) | **CONDITIONAL PASS** (see §7) |
| Named sign-off in decision record | **Pending** |
| Anchor / XChaCha24 / S-Class | **Open** |
| Real user-media durability review | **Open** |
| Service/UI wiring | **Absent** (correct for current phase) |
| **Writer Enable Readiness** | **NO-GO** |

## 7. `ExternalReviewCompleted` eligibility

| State | Applicable? |
|-------|-------------|
| `false` (code) | **YES — must remain** in this phase (no code constant changes) |
| **true-candidate only** (decision record draft) | **YES** — after **CONDITIONAL PASS**: security controls for disabled-writer posture are acceptable; **M-01/M-02** are non-security blockers for *review completion* but should be addressed in **E-9.1** before **approved** sign-off |
| **approved** (named sign-off) | **NO** — sign-off table empty |

**Reviewer instruction:** Do **not** flip `ExternalReviewCompleted` in `Av3PhaseGate.cs` until decision record carries **approved** named sign-off **and** product owner accepts E-9.1 documentation/CI items (or waives M-01/M-02 in writing).

## 8. Remaining blockers (enable — unchanged)

1. Named formal sign-off on `ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md`
2. `ProductionAnchorImplemented` — not implemented
3. `XChaCha24Implemented` — not implemented; ChaCha12 transitional **BELOW S-CLASS**
4. `SClassTargetSatisfied` — false
5. `ActualDiskDurabilityReviewed` / release durability — false
6. Phase H migration — separated; not implemented
7. Explicit approved design for service/UI/import-export wiring (currently absent)

## 9. Verification results (this review run)

| Command | Result |
|---------|--------|
| `dotnet format --verify-no-changes` | **PASS** (exit 0) |
| `dotnet build SmartPerformanceDoctor.sln -c Release -p:Platform=x64` | **PASS** (0 errors, 0 warnings) |
| `dotnet test` full Release x64 | **PASS** — **388** passed, **0** failed, **0** skipped (~1m 18s) |
| Filter: `Av3PhaseE9\|Av3PhaseGate\|Av3PhaseE8\|Av3PhaseE7\|DryRun\|Invariant\|Fault\|Cancellation` | **PASS** — **114** passed, **0** failed (~44s) |

## 10. Sign-off decision (formal)

| Field | Value |
|-------|-------|
| **Formal External Review** | **CONDITIONAL PASS** |
| **Critical Findings** | **0** |
| **High Findings** | **0** |
| **Medium Findings** | **2** (M-01, M-02) |
| **Rationale** | Disabled-writer gate/isolation/invariant/confidentiality posture **meets** pre-enable external review bar; **no** production authorization. Documentation test-count drift and harness guard CI hygiene require **E-9.1** before **approved** sign-off. |
| **Production Writer** | **NOT AUTHORIZED** |
| **Writer Enable Readiness** | **NO-GO** |
| **ExternalReviewCompleted (code)** | **false** (unchanged) |
| **ExternalReviewCompleted (draft)** | **true-candidate only** after product accepts CONDITIONAL PASS + E-9.1 plan |
| **Next Phase** | **E-9.1 Review Fixes** (M-01, M-02), then **E-10 Enable Decision Gate** — **not** production enable |

---

*This report does not set `ExternalReviewCompleted=true`, `WriterEnableReady=true`, or any production enable flag in code.*

## 11. E-9.1 closure (Medium findings)

| ID | Status | E-9.1 action |
|----|--------|--------------|
| M-01 | **CLOSED** | E-DOC-SOT + doc consistency tests; latest **455/455** + **191/191**; historical 444/388 noted |
| M-02 | **CLOSED** | `Av3HarnessCommitGuardRegistry` + `IAv3CommitGuardLease`; per-root purge; `Av3PhaseE91Tests` parallel isolation |

See `ASTRA_VAULT_E91_REVIEW_FIXES_REPORT.md`.

## 12. Named sign-off (E-10 entry — 2026-07-06)

- **Record:** `ASTRA_VAULT_NAMED_SIGNOFF_RECORD.md`
- **Posture:** **CONDITIONAL PASS ADDRESSED**; M-01/M-02 **CLOSED**; Critical/High **0**
- **Effect:** E-10 Enable Decision Gate **may proceed**; **NOT** production writer enable; code `ExternalReviewCompleted` **unchanged false**