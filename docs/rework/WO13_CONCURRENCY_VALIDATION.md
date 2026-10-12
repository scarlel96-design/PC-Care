# WO-13 concurrency validation

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `7E468DC4BFC6735757E6994CAAB8D002E1A94EE1E72FDC88A6F556C27263E7AC`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Implemented coordination

A reentrant per-service Monitor lease serializes public synchronous vault operations.
Create also uses an exclusive filesystem creation lease.
Vault session ownership uses the existing exclusive file boundary.
Lock waits for an active import in the same service instance.
The import barrier test released the operation and verified the seed after reopening.
Two service instances demonstrated session conflict and recovery-code preservation.

## Unqualified cases

The two-instance test runs in one process. It is not a simultaneous two-process recovery-code race test.
The crash harness uses separate processes, but it does not cover every concurrent operation pair.
Create versus Create, Export versus PasswordChange, and Dispose during cancellation require broader live race tests.
Monitor serialization can delay UI callers when callers invoke synchronous operations on the UI thread.
The existing task dispatch remains necessary.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
