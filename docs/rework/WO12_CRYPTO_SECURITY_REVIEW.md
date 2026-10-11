# WO-12 cryptographic security review

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

## Existing contracts

Legacy SecureVaultCrypto supports Argon2id and legacy PBKDF2-SHA512.
It uses existing AES-GCM and chunked formats, not a new WO-12 cipher design.
Lab uses Argon2id, random salt and independent master/data keys.
Lab wraps keys and metadata with AES-GCM. Content uses existing XChaCha20-Poly1305 composition.
The HChaCha20 component is repository code. The final ChaCha20Poly1305 operation uses .NET.
Salt is 32 bytes. XChaCha content nonces use 24 bytes. AES-GCM nonces use 12 bytes. AEAD tags use 16 bytes.
HKDF labels separate key purposes. Random nonces are not a proven persistent global uniqueness ledger.
Lab memory parameters clamp to at least 64 MiB. LabFast measurements do not represent production KDF performance.

## Improvements

Create now clears temporary master-key, salt and wrapping-key arrays in finally blocks.
Import/export buffers and borrowed keys are cleared at the implemented ownership boundaries.
Duplicate independent sessions fail closed through a Windows lease.
Read-only unlock skips journal reconciliation and automatic metadata/header rewriting.

## Unresolved issues

Managed password strings cannot be reliably erased.
Whole-file plaintext arrays increase memory and residual exposure.
Metadata and activation commit are not a single atomic transaction.
Activation markers and the journal are not equivalent to an external authenticated anti-rollback witness.
Read-only unlock still updates audit/rate-limit files. It is not an immutable forensic reader.
Journal error suppression and malformed-line handling need further review.
Recovery-code consumption and password-change interruption require atomic protocol tests.
New independent cipher vectors and external cryptographic audit were not completed.
No commercial security certification or percentage score is asserted.

## Approval values

ProductionWriterEnabled=false; JournalWriterEnabled=false; MigrationEnabled=false.
WriterEnableReady=false; ExternalReviewCompleted=false; ProductionEnableAuthorized=false.
The new regression asserts all six values. Lab product-host flags are separate values.
