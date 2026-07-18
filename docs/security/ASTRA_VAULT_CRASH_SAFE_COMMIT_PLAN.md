# Astra Vault Crash-Safe Commit Plan (Phase E-0 — LOCKED)

**Production writer:** NOT AUTHORIZED  
**Phase E-3:** `Av3DurableStorageHarness` + child-process kill FI exercise this flow in isolated `av3-e3-kill-*` temp roots only (not production writer).

**Phase E-4 (R1):** `Av3TornWriteSimulator` + `Av3AtomicWriteValidator` enforce that torn/partial artifacts never classify as trusted `NewGenerationOpen` without post-flush AEAD + commitment chain (harness only).
**Authoritative commit:** authenticated activation header durable flush **after** metadata.root + journal + objects per policy below.

## 1. Commit ordering (LOCKED)

1. Write new object/segment ciphertext (temp names or allocator slots).
2. Write new metadata graph blobs + metadata.root ciphertext (descriptor + AEAD).
3. Write journal descriptor (+ optional AEAD body) marking progress.
4. Update **activation plaintext** fields (generation, commitments, digests).
5. Write activation ciphertext to **all** header copies (or rotate copies per policy).
6. **Flush** objects → metadata → journal → header copies (see §3).
7. **Post-flush reread** each flushed region.
8. **Post-flush authentication** (AEAD + digest + generation rules).
9. Transition to `Committed`; optional cleanup of superseded temp files (never delete previous generation until auth passes).

## 2. Atomic write strategy (LOCKED)

| Artifact | Strategy |
|----------|----------|
| Object blobs | Write to temp object id → `fsync` → rename/swap pointer in metadata only after blob auth |
| metadata.root | Write new file `metadata.root.enc.new` → `fsync` → replace locator/index pointer in activation only after AEAD verify |
| Journal | Append or replace generation-scoped journal file; `fsync` before activation write |
| Header copy | Write to inactive copy index first; `fsync`; then remaining copies; never overwrite last sole authenticated copy without redundant copy |

No in-place truncate of authenticated header region without full redundant copy.

## 3. Flush policy (LOCKED)

- **Windows:** `FileStream.Flush(flushToDisk: true)` + `FlushFileBuffers` on vault volume handle where applicable.
- **Order:** data blobs → metadata → journal → header copies (children before parents that reference them).
- **Failure:** if any flush fails → `Aborted`; previous generation remains open; classify per matrix.

## 4. Post-flush reread and authentication (LOCKED)

After each flush stage (minimum: metadata.root + activation header):

1. Reread exact byte range from disk.
2. Recompute ciphertext digests; compare to header/journal commitments.
3. Re-run AEAD decrypt + canonical plaintext validation (metadata.root).
4. Re-run activation AEAD + digest match.
5. On any failure → `CorruptBlocked` or `RedundancyDegraded` (if partial copies); **do not** expose new generation to UI.

## 5. Crash recovery matrix

| Scenario | Classification |
|----------|----------------|
| Object write **before** crash (not flushed) | **previous generation open** |
| Object write **during** crash | **previous generation open** |
| Metadata write **before** crash | **previous generation open** |
| Metadata write **during** crash | **previous generation open** |
| Journal write **during** crash | **previous generation open** |
| Object flush **failure** | **previous generation open** / **Aborted** |
| Metadata flush **failure** | **previous generation open** / **Aborted** |
| Activation header write **before** crash | **previous generation open** |
| Activation header write **during** crash | **recovery required** |
| Activation header flush **failure** | **recovery required** / **redundancy degraded** |
| Post-flush reread **failure** | **corrupt blocked** |
| Post-flush AEAD auth **failure** | **corrupt blocked** |
| Header copy **1** durable only | **redundancy degraded** (open if auth passes on that copy) |
| Header copy **2** durable | **new generation open** if consensus + auth |
| Header copy **3** **conflicting** | **corrupt blocked** / **rollback suspected** |
| Disk **full** | **Aborted**; **previous generation open** |
| External drive **removal** | **Aborted**; **previous generation open** if media returns |
| Stale **high** generation (unauthenticated) | **previous generation open** |
| Equal generation **conflicting** root | **corrupt blocked** |
| Old generation **rollback** attempt | **rollback suspected** |
| Cleanup **during** crash | **previous generation open**; temp orphans acceptable |

## 6. Writer state machine (authoritative)

| State | Entry | Writes | Flush required | On failure | Preserve old gen? | UI (public) | Log OK | Log forbidden |
|-------|-------|--------|----------------|------------|-----------------|-------------|--------|----------------|
| **Idle** | session start | none | no | — | yes | Locked / Ready | state=Idle | secrets |
| **Preparing** | user commit | temp alloc | no | → Aborted | yes | Preparing… | txn id, gen | paths, passwords |
| **WritingObjects** | after prepare | object blobs | no | → Aborted | yes | Writing… | object count | filenames |
| **WritingMetadata** | objects done | metadata + root | no | → Aborted | yes | Writing… | digest hex prefix | plaintext |
| **WritingJournal** | metadata staged | journal | no | → Aborted | yes | Writing… | journal state | user metadata |
| **FlushingObjects** | journal staged | — | objects | → Aborted | yes | Saving… | flush stage | — |
| **FlushingMetadata** | objects flushed | — | metadata | → Aborted | yes | Saving… | flush stage | — |
| **WritingActivationHeader** | metadata flushed | header copies | no | → RollbackRequired | yes | Saving… | copy index | VMK |
| **FlushingActivationHeader** | header written | — | headers | → RedundancyDegraded | yes | Saving… | copy count | — |
| **PostFlushReread** | flush done | — | no | → CorruptBlocked | yes | Verifying… | byte lengths | ciphertext |
| **PostFlushAuthentication** | reread ok | — | no | → CorruptBlocked | yes | Verifying… | auth result class | keys |
| **Committed** | auth ok | cleanup temps | optional | → Idle | supersede old **after** auth | Unlocked (read-only until E+) | committed gen | — |
| **RedundancyDegraded** | partial copies | repair path TBD | — | manual | yes | Degraded redundancy | copy mask | — |
| **RollbackRequired** | gen conflict | none | no | manual recovery | yes | Recovery required | reason code | — |
| **Aborted** | user/error | none | no | → Idle | yes | Cancelled / Failed | abort reason | secrets |
| **CorruptBlocked** | auth fail | none | no | support path | yes | Corrupt blocked | public message only | oracle data |

**Implementation note:** E-0 defines transitions only; no runtime state machine code shipped.

## 7. Related

- `ASTRA_VAULT_WRITER_GATE.md`
- `ASTRA_VAULT_JOURNAL_MODEL.md`
- `ASTRA_VAULT_FAULT_INJECTION_PLAN.md`