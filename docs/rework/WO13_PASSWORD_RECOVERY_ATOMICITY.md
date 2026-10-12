# WO-13 password and recovery atomicity

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `7E468DC4BFC6735757E6994CAAB8D002E1A94EE1E72FDC88A6F556C27263E7AC`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Password transaction

The v6 password change retains the VMK and encrypted content.
A new salt and KEK wrap the same VMK in an independent authenticated generation.
The activation controls the accepted password generation.
Ten forced-termination cases reopened the vault in separate reader processes.
Pre-publication readers used the previous credential. Post-publication readers used the new credential.
Each reader verified the seed SHA-256.
The normal password-change test rejected the old password and accepted the new password.
The existing recovery code still opened the changed vault.
The forced-termination matrix did not try both passwords at every stage.
Recovery-code rotation remains blocked for v6 until its own authenticated transaction exists.

## Recovery-code consumption

Unlock reserves the vault session before recovery-code consumption.
It validates the recovered VMK, opens the authenticated session, then records consumption.
A cross-process consume lease protects record replacement.
Consumption uses a unique temporary file, flush, and previous-file backup.
A session conflict does not consume the code. Normal reuse is rejected.
The used-code bitmap remains unauthenticated and lacks a freshness anchor.
Consumption and process/session lifetime do not form one durable transaction.
Crash interruption, record replay, and actual simultaneous separate-process code use remain unresolved P0 checks.
The v5 password-header overwrite path remains an unresolved P0.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
