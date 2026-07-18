# Astra Vault Production Durable Writer Design (Phase E-5)

**Status:** DESIGN LOCKED — **NOT IMPLEMENTED** — **NOT AUTHORIZED**  
**AV3:** NOT PRODUCTION  
**Legacy spd-vault:** BELOW S-CLASS (on-disk unchanged)  
**S-Class:** NOT YET SATISFIED (XChaCha24 + trusted anchor + production writer FI pending)

Phase E-5 freezes the production durable writer **design package**. It does **not** enable `ProductionWriterEnabled`.

**Phase E-7 (pre-enable hardening):** Disabled `Commit/*` implementation remains harness-only. Invariants, multi-layer gates, and negative-route tests harden future enable — **still NOT AUTHORIZED**. No service/UI/import/export/migration wiring.

## 1. Writer scope

- Crash-safe commit of AV3 container artifacts: content objects/segments, encrypted metadata graph blobs, `metadata.root`, digest-only journal (`JNAL` v1), 3-copy activation headers, locator consistency.
- Operates only on **new** AV3 vault roots (Phase F+); does not mutate legacy spd-vault on-disk layouts.
- Uses E-3/E-4 harness evidence (durable flush, kill FI, torn write, header repair classification, rollback, journal confidentiality).

## 2. Non-goals

- Legacy spd-vault migration (Phase H; `MigrationEnabled=false`).
- User-facing import/export wiring (Phase F; no App service connection in E-5).
- Auto-delete of user originals or default secure-delete changes.
- Journal AEAD body in production v1 (digest-only policy; see R11).
- Production anchor I/O or XChaCha24 implementation (separate phases).
- Auto-repair of degraded header copies without operator/recovery flow.

## 3. Threat model

| Threat | Mitigation (design) |
|--------|---------------------|
| Partial/torn write (R1) | Temp-then-rename, sector alignment, post-flush reread + AEAD auth |
| Header redundancy loss (R2/R3) | 3-copy strategy; classify only — no silent merge |
| Generation rollback (R10) | Monotonic policy + optional anchor; fail-closed open |
| Journal leakage (R11) | Digest/UUID/generation/state only on disk |
| Crash mid-commit | State machine; journal precedes activation authority |
| Whole-vault restore | **Limitation:** without anchor, undetectable as legitimate old gen |
| Key material exposure | No secrets in logs/journal/anchor; session zeroize |

## 4. Writer state machine

Authoritative table: `ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md` §6.

`Idle` → `Preparing` → `WritingObjects` → `WritingMetadata` → `WritingJournal` → flush substates → `WritingActivationHeader` → `PostFlushReread` → `PostFlushAuthentication` → `Committed`.

Failure branches: `RedundancyDegraded`, `RollbackRequired`, `Aborted`, `CorruptBlocked`.

## 5. Object write strategy

- Random object/segment ids; ciphertext at rest; segment keys via HKDF(domain).
- Write to temp paths under vault root; commit via durable store after flush.
- AAD binds generation, container id, object/segment ids (see crypto model).
- Partial object writes never advance trusted generation.

## 6. Metadata root write strategy

- Plaintext metadata graph edits in memory only; persist as AEAD `metadata.root` blob.
- `target_generation` monotonic; commitment digests updated atomically with ciphertext.
- Equal-generation conflicting commitments → reject open (existing read-only rules).

## 7. Journal strategy

- v1: fixed 256-byte digest-only descriptor (`Av3JournalDigestOnlyPolicy`).
- States: durable journal before activation header becomes authoritative.
- Optional AEAD envelope deferred (`Av3JournalAeadEnvelope.ProductionEnvelopeEnabled = false`).

## 8. Activation header write strategy

- Update all three header copies with consistent activation AEAD + generation.
- Order: metadata + journal durable → header copies → post-flush auth of full chain.
- Torn header copy → `Av3HeaderRepairClassifier` outcome; no auto-trust of new gen.

## 9. 3-copy header update strategy

- Rotate/write copies per `Av3HeaderCopyWritePlan` (harness skeleton in E-3).
- Quorum/consensus read remains unlock-time; writer must not leave copies in conflicting equal-generation state.

## 10. Durable flush policy

- `FlushFileBuffers` (or equivalent) on vault root and each committed file before reread.
- Directory metadata durability where platform requires (see crash-safe plan §3).

## 11. Post-flush reread / authentication policy

- Reread committed bytes; verify AEAD tags for activation + metadata.root before `Committed`.
- Only path that may set `NewGenerationOpen` / trusted target generation (E-4 validator).

## 12. Old generation preservation policy

- Until post-flush authentication succeeds, **previous** generation is sole trusted open generation.
- Aborted commits leave orphan temps for GC policy (secure wipe) without promoting generation.

## 13. Crash recovery policy

- On reopen: journal + header observations → `IAv3RecoveryManager` / existing classifiers.
- Never trust torn new activation without authentication pass.

## 14. Rollback policy

- Local field rollback: detect via generation windows + digest consensus (`Av3RollbackDetector`).
- Full-vault rollback: see `ASTRA_VAULT_ANCHOR_MODEL.md`; without anchor → limitation documented.

## 15. Repair classification policy

- `Av3HeaderRepairClassifier` / `Av3RepairClassifier` — **classification and plan only** in production v1.
- Operator/recovery center (Phase I) for manual repair; no silent auto-fix.

## 16. No auto migration policy

- spd-vault → AV3 only via explicit Phase H wizard; writer does not read/write spd-vault paths.

## 17. No auto delete original policy

- Writer does not delete user source files; UI `sealOrigin` remains opt-in; legacy defaults unchanged.

## 18. Production enable conditions

All required before `ProductionWriterEnabled` may be considered (still needs decision record + review):

1. E-0–E-5 design locks remain satisfied.
2. E-3/E-4 FI harness green on release matrix.
3. Production writer implementation behind `IAv3VaultWriter` with full crash matrix pass.
4. External security review **completed** (not done in E-5).
5. XChaCha24 migration plan executed or explicit waiver (not done).
6. Anchor strategy implemented or accepted risk for v1 (not done).
7. `Av3EnableReadinessChecklist.WriterEnableReady` flipped only after above — **false** in E-5.

## 19. Rollback / disable plan

- Feature flag: `ProductionWriterEnabled` / `JournalWriterEnabled` compile-time gates.
- Runtime: host refuses writer DI registration when flags false.
- Disable: revert flags; read-only unlock remains; orphaned in-flight gens classified fail-closed.

## 20. API surface (design)

Recommended interfaces in `SmartPerformanceDoctor.AstraVault.WriterDesign`:

- `IAv3VaultWriter`, `IAv3WriteSession`, `IAv3TransactionCoordinator`, `IAv3DurableStore`, `IAv3HeaderCommitter`, `IAv3JournalRecorder`, `IAv3RecoveryManager`, `IAv3WritePolicy`

E-5: interfaces only. **E-6:** disabled implementations in `SmartPerformanceDoctor.AstraVault.Commit` (`Av3CommitOrchestrator`, durable store, header/journal/recovery/policy) — **harness-only**; production `CommitAsync` / factory route **blocked** while `ProductionWriterEnabled=false`. **Not** wired to App services or UI.

**E-8:** `SmartPerformanceDoctor.AstraVault.DryRun` — production-shaped end-to-end dry-run on `av3-e8-` / `av3-harness-` synthetic fixtures; read-only revalidation + telemetry scan. Dry-run `Committed` is harness-local evidence only — **does not** imply `WriterEnableReady` or production authorization.

### `IAv3WritePolicy` (design contract)

| Concern | Policy |
|---------|--------|
| Failure modes | Violation → reject commit before durable activation; no cleartext journal / migration bypass |
| Secret handling | No key material on policy object; no paths/passwords in logs |
| Logging | Container id, generation, public error class only |
| Cancellation | Immutable per session; abort handled by `IAv3TransactionCoordinator` |
| Sync/async | Sync property reads at session/commit boundaries |
| Idempotency | Tied to format/policy version, not per retry |
| Testability | Injectable fake; defaults match non-goals §2 |
| Production enable | `ProductionWriterEnabled` + `ExternalReviewCompleted` — **false** (E-5.1) |

## Related

- `ASTRA_VAULT_PRODUCTION_WRITER_REVIEW_PACKAGE.md`
- `ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md`
- `ASTRA_VAULT_WRITER_GATE.md`
- `ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md`