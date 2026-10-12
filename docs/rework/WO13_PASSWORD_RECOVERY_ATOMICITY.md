# WO-13 password and recovery atomicity

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
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
The explicit v6 store authenticates slot state with a VMK-derived HMAC-SHA256 key.
Authentication covers the used bitmap, vault identity, wrapping data, and KDF parameters.
The reader bounds KDF parameters before deriving the authentication key.
Four separate-process interruption tests checked consumption before and after record replacement.
Each test reopened the vault with its password and verified the seed SHA-256.
Two concurrent processes produced one successful consumption and one rejection.
A missing v6 store cannot fall back to the unauthenticated v5 store.
The previous store is preserved as evidence. It is not an automatic recovery source.
Consumption and process/session lifetime do not form one durable transaction.
A code can be consumed before the interrupted caller receives success.
Whole-store replay lacks an external freshness anchor.
The v5 used-code bitmap remains unauthenticated.
The v5 password-header overwrite path remains an unresolved P0.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
