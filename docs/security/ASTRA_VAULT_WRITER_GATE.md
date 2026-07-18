# Astra Vault Writer Gate (Phase E-0)

**Status:** DESIGN GATE ONLY — **production writer NOT AUTHORIZED**  
**AV3:** NOT PRODUCTION / READ-ONLY VALIDATION  
**Legacy spd-vault:** BELOW S-CLASS (unchanged on-disk)  
**S-Class target:** NOT YET SATISFIED

Phase E-0 does **not** implement AV3 container writing. It locks prerequisites before any `ProductionWriterEnabled` flip.

**Phase E-3 (완료, test-only):** Windows child-process kill FI, `Av3DurableStorageHarness`, 3-copy header writer **skeleton**, repair **classification only**.

**Phase E-4 (완료, High Risk Closure Gate):** R1–R3/R10/R11 harness. **NOT** writer enable.

**Phase E-5 (완료, Production Writer Design Package):** `ASTRA_VAULT_PRODUCTION_WRITER_DESIGN.md`, review package, `WriterDesign/IAv3*` interfaces. **NOT** writer enable.

**Phase E-6 (완료, Disabled Production Writer Implementation):** `Commit/Av3CommitOrchestrator` + pipeline/durable/header/journal/recovery/policy types implement `IAv3*` **only on harness route** (`TestHarnessInvocation` + `av3-e*` root). `Av3WriterHarnessFactory.TryCreateProductionRoute()` **always fails** while `ProductionWriterEnabled=false`. **No** SecureVaultService / AstraVaultHostService / UI wiring. **NOT** production-ready. **Anchor / XChaCha24:** **not implemented**. **S-Class:** NOT YET SATISFIED.

**Phase E-7 (완료, Pre-Enable Hardening):** Multi-layer fail-closed gates (`Av3PhaseGate`, `Av3WriterAccessGate`, factory, orchestrator, session, coordinator, journal, durable store, policy). Production route negative matrix tests. `Av3WriterInvariant*` contract. Cancellation / concurrency / reentrancy harness. Recovery/repair **classification only** — no auto-mutation. **Still NOT AUTHORIZED** for production writes; `WriterEnableReady=false`; `ExternalReviewCompleted=false`.

**Phase E-9 (완료, External Sign-off Prep):** External-review-ready package — `ASTRA_VAULT_EXTERNAL_REVIEW_CHECKLIST.md`, `ASTRA_VAULT_EXTERNAL_REVIEW_EVIDENCE_INDEX.md`, refreshed review package. **NOT** `ExternalReviewCompleted`; **NOT** writer enable.

**Phase E-9.1 (완료, Review Fixes):** M-01/M-02 **CLOSED**. **NOT** writer enable.

**Named sign-off record (준비됨):** `ASTRA_VAULT_NAMED_SIGNOFF_RECORD.md` — human sign-off **pending**; code `ExternalReviewCompleted=false`.

**Phase E-10 (완료, Enable Decision Gate):** **Production Enable NO-GO** (`ProductionEnableAuthorized=false`).

**Phase E-11 (완료):** Harness anchor package. **E-11.1 (완료):** B-1 **PARTIAL / SIGNED CANDIDATE**; `E111AnchorSignoffGateComplete=true`; `ProductionAnchorImplemented=false`.

**Phase E-12 (완료):** XChaCha24 harness closure package. **E-12.1 (완료):** Crypto sign-off — B-2 **APPROVED CANDIDATE**; `XChaCha24SignoffApprovedCandidate=true`; `XChaCha24Implemented=false`.

**Phase E-13 (완료):** Trusted anchor provider implementation (`av3-e13-` harness). **E-13.1 (완료):** Sign-off — B-1 **PARTIAL / SIGNED CANDIDATE**; `E131TrustedAnchorSignoffGateComplete=true`; `ProductionAnchorImplemented=false`; live witness **없음**. **Phase E-14 (완료):** Disk durability review (`av3-e14-` harness); `ActualDiskDurabilityReviewCandidate=true`; `ActualDiskDurabilityReviewed=false`; production writer **NOT AUTHORIZED**. Next: **E-14.1** disk durability sign-off.

**Phase E-8 (완료, Limited Dry-Run):** `DryRun/Av3DryRunRunner` — synthetic fixture, production-shaped pipeline on `av3-e8-`/`av3-harness-` temp roots only; read-only revalidation; fault matrix; telemetry scan. **NOT AUTHORIZED** for production; dry-run success ≠ `WriterEnableReady`.

**Phase E-7.1 (완료, Review Fixes):** E-7 external engineering review P1/P2 — `ValidateTrustedGenerationPreserved` enforced; extended negative matrix (migration, session, header, journal gate); FailCleanup ↔ invariant linkage; `JournalWriterEnabled` in closed-gate checks; **strict harness root** (OS temp + `av3-harness-` / `av3-e7-` / `av3-e71-` / `av3-e8-` / `av3-e6-` / `av3-e62-` / `av3-e3-` prefixes; reject Documents/Desktop/Downloads and path escape); caller `CancellationToken` tests; repair classifier storage non-mutation tests. **Writer enable NO-GO**; `ExternalReviewCompleted=false`.

## 1. Purpose

Define mandatory conditions, state machine, journal model references, crash matrix, and fault-injection plan so Phase E+ implementation cannot proceed without explicit gate review.

## 2. Writer enable conditions (all required)

| # | Condition | Phase E-0 status |
|---|-----------|------------------|
| 1 | Locator / header / activation / metadata.root golden vectors **LOCKED** | **SATISFIED** (`test-vectors/av3`, Phase C–D) |
| 2 | Metadata graph on-disk layout **LOCKED** | **SATISFIED** (design §3; implementation deferred) |
| 3 | Journal state model **LOCKED** | **SATISFIED** (`ASTRA_VAULT_JOURNAL_MODEL.md`) |
| 4 | Atomic write strategy **LOCKED** | **SATISFIED** (`ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md` §2) |
| 5 | `fsync` / `FlushFileBuffers` policy **LOCKED** | **SATISFIED** (crash-safe plan §3) |
| 6 | Post-flush reread + authentication rule **LOCKED** | **SATISFIED** (crash-safe plan §4) |
| 7 | Crash recovery matrix **LOCKED** | **SATISFIED** (`ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md` §5) |
| 8 | Fault injection test plan **LOCKED** | **SATISFIED** (`ASTRA_VAULT_FAULT_INJECTION_PLAN.md`) |
| 9 | Rollback / generation policy **LOCKED** | **SATISFIED** (read-only rules + writer gate §4) |
| 10 | Old generation preservation policy **LOCKED** | **SATISFIED** (§4) |
| 11 | Secure temp policy **LOCKED** | **SATISFIED** (§5) |
| 12 | No auto-delete original policy **LOCKED** | **SATISFIED** (§5) |
| 13 | Legacy migration policy **separated** | **SATISFIED** (Phase H only; `MigrationEnabled=false`) |
| 14 | User data destructive action **not default** | **SATISFIED** (§5) |

**Gate flip rule:** `Av3PhaseGate.ProductionWriterEnabled` may become `true` only after:

- All rows above remain satisfied,
- Fault-injection harness exists and passes (Phase E+),
- Independent security review sign-off (out of scope for E-0).

## 3. Metadata graph layout (LOCKED design, not implemented)

- **Root of trust:** authenticated activation header + metadata.root AEAD (Phase D).
- **Graph:** encrypted metadata nodes / index nodes referenced by digests in metadata.root plaintext (no filename/path/plaintext manifest in journal).
- **Materialization:** unlock-time only after full chain; writer may update graph blobs but must not write cleartext paths or names to disk.
- **Object binding:** content segments use random object/segment ids; AAD binds generation and commitments.

Implementation of graph writer is **NOT AUTHORIZED** in E-0.

## 4. Generation and rollback policy (writer)

- **Monotonic metadata generation:** `target_generation > previous_generation` for normal commit.
- **Equal generation:** conflicting `metadata_root_plaintext_commitment` or `metadata_root_ciphertext_digest` across header copies → **reject open** (existing read-only rules).
- **Rollback suspected:** `target_generation <` last durable header generation, or journal claims forward commit while activation not authenticated → classify **RollbackRequired** / **CorruptBlocked** (see crash matrix).
- **Old generation preservation:** until `Committed` + post-flush authentication succeeds, previous generation remains the only **trusted** open generation; partial new blobs are **not** authoritative.

## 5. Secure temp and destructive defaults

- Temp decrypt/export paths: ACL-restricted, wiped on lock/abort; no world-readable directories.
- **Default:** import/seal does **not** delete user originals (`sealOrigin` UI opt-in only; legacy default unchanged).
- **No** secure-delete or origin removal as silent writer side-effect.
- Writer logs: container id, generation, state machine transition, public error class — **no** password, VMK, DEK, plaintext paths, or filenames.

## 6. Writer state machine

Authoritative transition table: `ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md` §6 (states listed in Phase E-0 spec).

Summary states: `Idle` → `Preparing` → `WritingObjects` → `WritingMetadata` → `WritingJournal` → flush substates → `WritingActivationHeader` → `PostFlushReread` → `PostFlushAuthentication` → `Committed`; failure branches: `RedundancyDegraded`, `RollbackRequired`, `Aborted`, `CorruptBlocked`.

## 7. Code gates (`Av3PhaseGate`)

| Flag | E-0 value | Meaning |
|------|-----------|---------|
| `WriterDesignLocked` | `true` | This document + state machine frozen |
| `CrashSafeCommitLocked` | `true` | Crash-safe plan frozen |
| `JournalModelLocked` | `true` | Journal model frozen |
| `FaultInjectionPlanLocked` | `true` | FI plan frozen |
| `ProductionWriterEnabled` | **`false`** | No production writer |
| `JournalWriterEnabled` | **`false`** | No journal writer |
| `MigrationEnabled` | **`false`** | No migration |
| `HighRiskClosureGateLocked` | **`true`** | E-4 R1/R2/R3/R10/R11 harness frozen (not writer enable) |
| `HighRiskClosureHarnessEnabled` | **`true`** | Test-only closure harness (documentation) |

## 8. Related documents

- `ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md`
- `ASTRA_VAULT_JOURNAL_MODEL.md`
- `ASTRA_VAULT_FAULT_INJECTION_PLAN.md`
- `ASTRA_VAULT_DATA_LOSS_RISK_REGISTER.md`
- `ASTRA_SECURE_CONTAINER_FORMAT.md` (read-only byte spec)