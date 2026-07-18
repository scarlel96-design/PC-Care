# Astra Vault Journal Model (Phase E-0 — LOCKED design)

**Implementation:** E-1 descriptor parse/validate (`Av3JournalDescriptor`) — **JournalWriterEnabled false**
**Root of trust:** authenticated **activation header** (not journal alone)

Journal assists crash recovery and commit ordering; it must **not** store cleartext filenames, paths, or user metadata.

## 1. Journal descriptor (fixed header — design lock)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | magic `JNAL` |
| 4 | 2 | struct_version (=1) |
| 6 | 2 | cipher_suite_id |
| 8 | 16 | container_id |
| 24 | 16 | transaction_id (UUID) |
| 40 | 8 | previous_generation |
| 48 | 8 | target_generation |
| 56 | 32 | previous_metadata_root_ciphertext_digest |
| 88 | 32 | target_metadata_root_ciphertext_digest |
| 120 | 32 | object_write_set_digest |
| 152 | 32 | metadata_write_digest |
| 184 | 4 | activation_target (=1 metadata_root) |
| 188 | 4 | journal_state (enum u32) |
| 192 | 8 | monotonic_timestamp_utc (policy: UTC tick or unix ms; clock not trusted for crypto) |
| 200 | 32 | journal_record_digest (SHA-256 of bytes 0..200) |
| 232 | 24 | reserved (zero) |

**Trailing payload:** AEAD-protected journal body (optional in v1; if present, same VMK/metadata domain policy as format spec — details in Phase E implementation).

**Phase E-2 harness:** `Av3JournalDigest` record digest verified on every simulated commit before flush; journal is hint-only until activation AEAD passes (unchanged root-of-trust model).

**Phase E-4 (R11):** **Policy decision (v1):** digest-only fixed descriptor — **no** cleartext paths/filenames/extensions/password/VMK in journal. Optional AEAD journal body **deferred** (`Av3JournalAeadEnvelope` not enabled). Journal is **not** root of trust.

**Phase E-6.1 (R11 stabilization):** Binary **structural** scan (`Av3JournalConfidentialityScanner` + `Av3JournalBinaryFieldPolicy`) validates JNAL layout, record digest, and digest field sizes — **raw digest bytes are not UTF-8 token-scanned**. **Textual** leak scan (`Av3JournalTextualLeakScanner`) applies only to trace/report/exception/debug strings. Trailing cleartext appendix after 256-byte descriptor is rejected. Deterministic test fixtures (`Av3JournalDeterministicFixtures`) remove RNG false-positives. `JournalWriterEnabled` remains **false**; production writer **NOT AUTHORIZED**.

**Phase E-6.2 (API footgun):** `Av3JournalLeakScanner.ScanUtf8` forwards to **`ScanUtf8TextualSurface`** — **textual UTF-8 surfaces only** (reports, traces). **Never** use UTF-8 leak scan on JNAL binary; use `Av3JournalConfidentialityScanner` for on-disk journal bytes. Harness `Av3CommitJournalRecorder.BuildDigestOnlyJournal` uses deterministic fixture digests; CSPRNG for live writer digests is deferred until writer enable GO.

### Journal state enum (u32)

| Value | Name |
|-------|------|
| 0 | `Pending` |
| 1 | `ObjectsDurable` |
| 2 | `MetadataDurable` |
| 3 | `JournalDurable` |
| 4 | `ActivationPending` |
| 5 | `Committed` |
| 6 | `Aborted` |
| 7 | `Stale` |

## 2. Authentication policy

- Journal digest field is **integrity hint only**; trust requires activation header AEAD + metadata.root AEAD success.
- Journal body (if encrypted): AEAD with AAD binding `container_id | transaction_id | target_generation | journal_state | record_digest`.
- **Truncation / partial write:** parser fail-closed → treat as **no commit**; previous generation remains authoritative.
- **Replay:** journal with `target_generation` ≤ last authenticated header generation and mismatching roots → **stale journal reject**.

## 3. Stale journal reject

Reject journal when:

- `container_id` ≠ locator,
- `struct_version` unsupported,
- `target_generation < previous_generation`,
- `journal_state` = `Stale` or `Aborted`,
- record digest mismatch,
- timestamp rollback without matching generation chain (suspected tamper).

## 4. Rollback suspected

Classify **RollbackRequired** when:

- Durable header copies disagree on generation with **authenticated** lower copy and **unauthenticated** higher copy,
- Journal claims `Committed` but activation payload fails AEAD,
- metadata.root digest in journal ≠ header consensus digest.

## 5. Recovery rules (read-only classifier)

1. Parse locator (untrusted bounds only).
2. Authenticate header copies (Phase B–D pipeline).
3. If journal present, parse descriptor; **do not** trust graph/manifest from journal.
4. If activation authenticates and matches journal `target_*` fields → allow **new generation open**.
5. Else → **previous generation open** or **recovery required** per crash matrix.

## 6. Writer recording rules

- Journal records **digests and generation**, not object names.
- Object write set digest = hash over ordered list of `(object_id, segment_id, ciphertext_digest)` tuples.
- Metadata write digest = hash over metadata blob digests written in transaction.

## 7. Phase boundary

No journal writer code in repository until `JournalWriterEnabled` gate (separate from E-0 design lock).