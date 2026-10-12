# WO-13 concurrency validation

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
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

The two-instance session-conflict test runs in one process.
An additional live two-process recovery-code test returned exactly one success and one rejection.
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
