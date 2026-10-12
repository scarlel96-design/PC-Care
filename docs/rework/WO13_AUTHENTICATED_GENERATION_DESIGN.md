# WO-13 authenticated generation design

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Implemented contract

`LabAuthenticatedGeneration` stores immutable GUID-named header, metadata, and manifest files in `generations-v6`.
The activation contains vault ID, generation, file hashes, and HMAC-SHA256.
The HMAC uses a VMK-derived HKDF key with a separate activation context.
SHA-256 alone identifies bytes. The activation MAC authenticates their relationship.
Preparation writes each file exclusively, flushes, reopens, checks hashes, and verifies the MAC.
Publication replaces `active.json` and retains `previous.json`.
Each replacement is a file operation. Multiple generation writes are not one atomic filesystem transaction.
Recovery considers only published active and previous candidates.
A larger generation number does not authorize adoption.
Authenticated previous recovery is read-only when active data is invalid or a transaction remains incomplete.
Invalid candidates preserve encrypted objects and block writes.

## Remaining boundaries

Activation has no external freshness witness. Replay of a complete valid old generation remains a risk.
Pointer replacement still uses path-based File.Replace after parent validation.
Parent pinning reduces replacement opportunities. It does not prove every TOCTOU interval is closed.
Directory metadata persistence and physical power-loss durability remain NOT TESTED.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
