# WO-13 product readiness

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Outcome

The v6 experiment repairs the reproduced MetadataReady interruption through authenticated previous-generation access.
It validates password-transition recovery at ten process-termination points.
Streaming export, journal failures, source preservation, and operation serialization have regression coverage.
The default v5 product route does not gain the complete v6 commit protocol.
WO-13 is PARTIAL. Phase 1 remains NO-GO.

| Requirement | Status | Basis |
|---|---|---|
| MetadataReady v6 reader and bytes | PASS | Separate reader process and SHA-256 |
| Existing product v5 interruption | FAIL | Baseline preserved but locked |
| v6 password interruption | PASS | Ten forced-termination readers |
| Journal fail-visible contract | PARTIAL | Lock and malformed records; disk-full untested |
| Same-instance serialization | PARTIAL | Import versus Lock barrier test |
| Recovery-code durable atomicity | PARTIAL | v6 MAC and four process interruptions passed; replay and v5 remain |
| Original file preservation | PASS | Both explicit source-removal routes blocked and tested |
| Streaming file integrity | PASS | Five sizes through 256 MiB |
| Low-spec memory qualification | NOT TESTED | No representative hardware |
| Actual GUI | NOT TESTED | Dedicated account absent |
| Physical power-loss durability | NOT TESTED | No VM or equipment trial |
| External cryptographic review | BLOCKED | No independent reviewer |
| AV3 production approval | BLOCKED | All six approval gates false |

## Follow-up priority

Complete v5 compatibility and recovery-store replay protection before considering production approval.
Extend concurrency, mid-write termination, interrupted recovery, and filesystem attack tests.
Run dedicated-profile GUI and VM power-off trials.
Optimize chunk allocations after preserving authentication and byte equality.
Retain existing updater, signing, ACL, UAC, reboot, and storage-device release blockers.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.

## Final Windows CI

Run `38200891260` passed on source `5573de4c6c982731e713a074f6ca1b26532f8b46`.
Application: 1,055 PASS / 111 SKIP / 0 FAIL. SecurityLab: 36 PASS / 0 SKIP / 0 FAIL.
The downloaded artifact independently confirms the TRX counters and synthetic reader results.
Read `evidence/wo13/CI_RESULT.json` for file hashes and `GITHUB_RESULT.txt` for command receipts.
