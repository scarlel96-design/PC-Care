# WO-13 crash recovery

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `7E468DC4BFC6735757E6994CAAB8D002E1A94EE1E72FDC88A6F556C27263E7AC`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Verified scope

Command: `dotnet run --project tools/SmartPerformanceDoctor.VaultProbe -c Release -- --crash-safe artifacts/wo13/final`. Exit code: 0.
Literal output: `CRASH_SCENARIOS=12;PASSWORD_CRASH_SCENARIOS=10;STREAMING_CASES=5;EXISTING_BYTES=PASS;METADATA_READY=PASS;FORMAT=v6-test-only;POWER_LOSS=NOT_TESTED`.
The harness used generated fixture roots and synthetic seed data.
It terminated a writer process at each barrier and launched a new reader process.
Reader exit 0 requires decrypted content bytes to match the seed SHA-256.
Ciphertext preservation alone cannot satisfy this condition.
MetadataReady opened the authenticated previous generation in read-only mode.
The committed generation contains the seed and the new file after activation publication.
The previous generation contains only the seed before publication.

## Limits

The matrix covers completed object writes and completed generation writes.
Interruption halfway through a low-level write remains NOT TESTED.
Recovery interrupted by another termination remains NOT TESTED.
VM forced power-off and physical storage power loss remain NOT TESTED.
The old v5 MetadataReady failure stays PreservedButLocked.
It is not reclassified by the successful v6 experiment.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
