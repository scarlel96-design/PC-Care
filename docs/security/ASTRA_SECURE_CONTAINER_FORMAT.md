# Astra Secure Container Format v3 (Phase B byte spec)

**Status:** NOT PRODUCTION / READ-ONLY VALIDATION (parser + unlock validator only).  
**Phase D:** metadata.root ciphertext AEAD read-only validation (no graph/manifest materialization).  
**Phase E-0:** writer / journal / crash-safe commit **design gate only** — see `ASTRA_VAULT_WRITER_GATE.md`. **Writer NOT AUTHORIZED.**  
**Phase E-2:** fault injection harness + `Av3ExperimentalWriter` AEAD-backed simulation (test-only, isolated temp). **Production writer NOT AUTHORIZED.**
**Phase C:** Golden vectors locked under `test-vectors/av3/` (TEST ONLY). Writer/journal/migration **NOT AUTHORIZED**.

Magic: `AVLT` (0x41 0x56 0x4C 0x54)

## 1. vault.locator (fixed 512 bytes, v3.0)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | magic |
| 4 | 2 | major (must be 3) |
| 6 | 2 | minor |
| 8 | 16 | container_id |
| 24 | 2 | cipher_suite_id (1=CURRENT ChaCha20-Poly1305 12-byte nonce, 2=AES-256-GCM) |
| 26 | 2 | kdf_suite_id (1=Argon2id) |
| 28 | 8 | header_primary_offset |
| 36 | 8 | header_secondary_offset |
| 44 | 8 | header_tertiary_offset |
| 52 | 4 | header_copy_size (896–8192) |
| 56 | 456 | reserved (zero) |

Reject: wrong size, bad magic, unknown major, non-zero reserved, unsupported suite, header_copy_size out of bounds.

## 2. vault.header copy (`VHDR`, authenticated)

Fixed region **512 bytes** + `password_slot_count × 384` bytes.

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | magic `VHDR` |
| 4 | 2 | struct_version (=1) |
| 6 | 2 | flags |
| 8 | 8 | generation |
| 16 | 16 | container_id |
| 32 | 1 | copy_index (0=primary, 1=secondary, 2=tertiary) |
| 33 | 1 | password_slot_count (1–2) |
| 34 | 2 | cipher_suite_id |
| 36 | 24 | default Argon2id descriptor |
| 60 | 32 | activation_payload_sha256 |
| 92 | 32 | metadata_root_plaintext_commitment |
| 124 | 32 | metadata_root_ciphertext_digest |
| 156 | 8 | metadata_generation |
| 164 | 8 | parent_metadata_generation |
| 172 | 4 | activation_target (1=metadata_root) |
| 176 | 16 | vault_id |
| 192 | 12 | activation_nonce |
| 204 | 16 | activation_tag |
| 220 | 56 | activation_ciphertext (AEAD, matches activation plaintext size) |
| 284 | 228 | reserved (zero) |
| 512+ | N×384 | password slot envelopes |

### 3-copy selection (read-only)

1. Parse each non-zero locator offset within `header_copy_size`.
2. Drop malformed copies (bad magic, size mismatch, container_id mismatch).
3. Require **consensus** on `metadata_root_plaintext_commitment` and `metadata_root_ciphertext_digest` across structural copies.
4. Cryptographic validity: Argon2id KEK → VMK unwrap → **activation payload AEAD decrypt** → digest/commitment match.
5. **Highest generation alone is not trusted** — reject if metadata generation chain / digest disagrees.
6. Prefer highest generation among crypto-valid copies that pass generation/rollback rules.

Activation plaintext (56 bytes, hashed to field at 60):

`generation u64 | metadata_root_plaintext_commitment 32 | metadata_generation u64 | parent_metadata_generation u64`

## 3. Password slot envelope (fixed 384 bytes, `PSLT`)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | magic `PSLT` |
| 4 | 2 | slot_version (=1) |
| 6 | 2 | slot_id |
| 8 | 2 | cipher_suite_id |
| 10 | 2 | kdf_suite_id |
| 12 | 8 | generation |
| 20 | 16 | container_id |
| 36 | 32 | kdf_salt |
| 68 | 24 | Argon2id descriptor |
| 92 | 12 | wrap_nonce (ChaCha) or 12 AES-GCM |
| 104 | 16 | wrap_tag |
| 120 | 32 | wrapped_vmk ciphertext |
| 152 | 232 | reserved (zero) |

## 4. Argon2id KDF descriptor (24 bytes)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 2 | kdf_suite_id (=1) |
| 2 | 2 | profile_id (1=standard, 2=low-memory) |
| 4 | 4 | memory_kib |
| 8 | 4 | iterations |
| 12 | 4 | parallelism |
| 16 | 8 | reserved (zero) |

Minimum: memory ≥ 65536 KiB, iterations ≥ 3, parallelism ≥ 1 — else reject (`KdfBelowMinimum`).

## 5. Activation payload AEAD (mandatory before trusting metadata)

- Key: `HKDF-SHA256(VMK, domain="astra-header-activation")`
- AAD: `format_version | container_id | vault_id | header_copy_id | header_generation | suite_id | metadata_root_digest | activation_target`
- Plaintext: 64-byte activation structure (see §2).
- **인증 실패 시** metadata root / generation / object / journal 신뢰 금지 (fail-closed).

## 6. VMK unwrap AAD

Little-endian packed:

`format_version u16 | aad_kind u16 (=1) | slot_id u16 | reserved u16 | generation u64 | container_id 16 | UTF-8 "astra-vmk-unwrap"`

## 7. Read-only unlock order (Phase D)

1. locator parse → 2. header 3-copy parse → 3. password slot parse → 4–5. KDF policy → 6. VMK unwrap → 7. activation payload AEAD → 8. metadata.root descriptor parse → 9. metadata ciphertext digest verify → 10. metadata.root AEAD → 11. metadata.root plaintext canonical verify → 12. root plaintext commitment verify → 13. generation/rollback verify → 14. `ReadOnlyUnlocked` (no metadata graph / manifest materialization).

## 9. metadata.root plaintext (512 bytes, `MRPL`)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | magic `MRPL` |
| 4 | 2 | version (=1) |
| 6 | 2 | cipher_suite_id |
| 8 | 8 | generation |
| 16 | 8 | parent_generation |
| 24 | 32 | graph_root_digest |
| 56 | 32 | allocation_root_digest |
| 88 | 32 | index_root_digest |
| 120 | 32 | journal_head_commitment |
| 152 | 32 | recovery_root_digest |
| 184 | 296 | reserved (zero) |
| 480 | 32 | root_plaintext_commitment = SHA-256(bytes 0..480) |

Reject: wrong size, bad magic/version, unsupported suite, reserved nonzero, trailing bytes, generation/parent mismatch, commitment mismatch, malformed digests.

## 10. metadata.root AEAD (read-only)

- Key: `HKDF-SHA256(VMK, domain="astra-metadata-root")`
- AAD: `format_version | suite_id | container_id | vault_id | header_generation | metadata_generation | metadata_ciphertext_digest | activation_payload_digest | metadata_root_logical_id | ciphertext_length | domain_len | "astra-metadata-root"`
- Ciphertext length must equal 512 for Phase D read-only root.
- **인증 실패 시** graph/index/journal/manifest 신뢰 금지 (fail-closed).

## 8. metadata.root.enc descriptor (first 256 bytes, `MROT`)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | magic `MROT` |
| 4 | 2 | struct_version (=1) |
| 6 | 2 | cipher_suite_id |
| 8 | 8 | generation |
| 16 | 8 | parent_generation |
| 24 | 16 | container_id |
| 40 | 32 | parent_root_hash |
| 72 | 32 | metadata_ciphertext_digest |
| 104 | 12 | nonce |
| 116 | 16 | tag |
| 132 | 4 | ciphertext_length (max 16 MiB) |
| 136 | 120 | reserved (zero) |

Generation/rollback: `metadata_generation ≥ parent_metadata_generation`; header copy digest must match descriptor; mixed generation open forbidden.

## 11. Phase boundaries (E-0)

| Allowed | Forbidden |
|---------|-----------|
| Locator/header/slot/metadata **parse** + read-only unlock | AV3 production **writer** |
| Golden vectors LOCKED | Journal **writer** |
| Writer gate **documentation** | Migration, graph materialization |
| PhaseGate tests | Changing spd-vault on-disk data |

## Reference

Phase C–D: golden vectors + read-only metadata root. Phase E-0: writer gate docs. Phase E+: writer implementation (**NOT AUTHORIZED** until FI + review).