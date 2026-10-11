# WO-12 data integrity

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

## Verified behavior

Exports create a random operation-owned temporary file through a pinned Windows file handle.
The writer flushes the file and validates its size and SHA-256.
The writer revalidates the parent directory before handle-based rename.
Rename does not replace an existing destination.
Failure cleanup removes only the operation-owned temporary file.
Foreign fixed-name temporary files remain preserved.
Imports preserve source files by default. The source scan rejects reparse ancestors and supports cancellation.
Lab file import holds a validated source handle and checks length/hash across collection.

## Measured payload

The performance fixture contains 4,194,304 deterministic synthetic bytes.
Input SHA-256 and extracted SHA-256 match:
`AAF34F2D9825929FA1021E4C0C7919F93F4B15343875E806678D6229E2CEFCE3`.
The folder test compares all four files, byte-for-byte, including their relative paths.
The crash tool compares every entry it can open in a new reader process.
MetadataReady cannot open. Its ciphertext-prefix preservation is not a successful plaintext recovery result.

## Limits

Folder operations can finish partially before a later collision or cancellation.
Already exported files remain present. The operation does not promise all-or-nothing folder rollback.
Directory creation and source enumeration retain broader concurrency review requirements.
Explicit legacy source sealing remains a separate destructive path; its default is disabled.
These results do not prove general user-vault compatibility or physical power-loss durability.

## Measurement boundary

The 539,525,120-byte peak belongs to the entire probe process.
The process also ran earlier crash fixtures and KDF operations.
It does not measure the isolated cost of one 4 MiB export.
Production KDF and low-end hardware performance remain NOT TESTED.

## Synthetic plaintext retention

VaultProbe retains generated plaintext exports beneath its private fixture root.
The private fixture location stays in an ignored local artifact.
No plaintext fixture is uploaded by the workflow.
Delete the generated fixture after review using its exact recorded path.
Ordinary deletion does not certify physical plaintext removal on SSD storage.
