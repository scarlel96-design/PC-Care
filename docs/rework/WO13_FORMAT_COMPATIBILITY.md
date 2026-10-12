# WO-13 format compatibility

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `7E468DC4BFC6735757E6994CAAB8D002E1A94EE1E72FDC88A6F556C27263E7AC`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


| Format | Current route | Write policy | Evidence |
|---|---|---|---|
| Legacy spd-vault | Existing product service | Source removal blocked | Synthetic folder round-trip regression |
| SecurityLab v4 | Existing compatible reader | Existing policy | Existing regression retained; no migration |
| SecurityLab v5 | Default Lab backend | Existing policy | Existing tests; MetadataReady P0 remains |
| SecurityLab v6 test | Explicit constructor option | Synthetic test path | Authenticated generations and 22 crash readers |
| AV3 | Existing gated implementation | Six production approval gates false | Gate and migration regression |

No implicit v4, v5, legacy, or AV3 conversion was added.
The product UI continues to create v5 through its existing backend.
The v6 constructor option is for isolated test fixtures.
Existing v5 objects already overwritten before this work have no guaranteed recovery.
Changing the default requires an approved compatibility and migration work order.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
