# WO-13 vault P0 root cause

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Baseline reproduction

Command: `dotnet run --project tools/SmartPerformanceDoctor.VaultProbe -c Release -- --synthetic artifacts/wo13/baseline`.
Input: Lab v5 synthetic seed and one synthetic import.
Exit code: 0. Harness output: `CRASH_SCENARIOS=3;SEPARATE_READER_PROCESSES=3;HASH_ROUNDTRIP=PASS;FAIL_CLOSED=1;POWER_LOSS=NOT_TESTED`.
The harness accepts preserved-but-locked as a fail-closed outcome. It does not certify complete recovery.
ObjectsReady and CommitWritten readers verified bytes. MetadataReady returned reader exit 4.
The MetadataReady fixture retained ciphertext but did not unlock.
The original fixture copy remains under local `artifacts/wo13/baseline-fixture-preserved`.
The original result remains in `BASELINE_RESULT.txt`.

## Failure sequence

The v5 import writes encrypted objects, then replaces metadata before publishing the matching commit.
An interruption leaves different authenticated generations in the header, metadata, and commit marker.
The old metadata bytes are already overwritten. Journal preservation cannot recreate them.
The v6 experiment writes independent generations and authenticates their activation.
The product default remains v5. The v5 interruption is an unresolved P0.
Existing damaged v5 metadata has no guaranteed recovery path.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
