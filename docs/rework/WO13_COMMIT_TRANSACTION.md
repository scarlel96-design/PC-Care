# WO-13 commit transaction

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Implemented sequence

1. Acquire the service operation lease and vault session lease.
2. Record Prepared before creating the encrypted object.
3. Write the object with an exclusive handle and flush it.
4. Record ObjectsReady.
5. Write an independent encrypted metadata generation.
6. Write its authenticated header and manifest.
7. Verify the generation files and authentication.
8. Record MetadataReady.
9. Publish the activation and retain the previous pointer.
10. Record Committed.

Failures are visible. A failed import removes its in-memory entry and blocks further writes.
Encrypted orphan objects remain preserved.
Publication success followed by final journal failure remains a failure result.
A later reader validates committed bytes before reconciling journal references.

## Actual crash test

Command: `dotnet run --project tools/SmartPerformanceDoctor.VaultProbe -c Release -- --crash-safe artifacts/wo13/auth-final`. Exit code: 0.
Twelve child processes were terminated at distinct barriers.
Twelve independent readers exported and verified the existing seed bytes.
Eight pre-publication cases opened the previous generation.
Four post-publication cases opened the committed generation.
See `COMMIT_RECOVERY_RESULT.json` for every stage and reader output.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
