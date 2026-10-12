# WO-13 execution report

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `7E468DC4BFC6735757E6994CAAB8D002E1A94EE1E72FDC88A6F556C27263E7AC`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Implemented result

Added explicit test-only v6 authenticated generations, previous-state recovery, and password activation.
Added fail-visible journal validation and serialized service operations.
Moved recovery-code consumption after session reservation and authentication.
Added chunked authenticated export with verified final publication.
Blocked explicit original-removal requests and corrected stale AV3 mirror authorization.
Added 26 application regression cases and a Windows workflow.
No production migration, official release, or main merge was performed.

## Local facts not available from GitHub alone

Local generated fixtures use dedicated temporary directories. No real user vault was selected.
Baseline v5 MetadataReady preserved ciphertext but reader exit 4 blocked unlock.
The original failing fixture remains preserved locally.
The v6 harness ran 22 writer terminations and 22 independent readers.
Both full regression attempts in the restricted shell aborted at testhost connection startup.
These aborted runs are not test passes. Host reruns use a 300-second connection timeout.
The first source-preservation regression failed because the new test expected an extra export root folder.
The existing legacy bundle contract exports relative paths directly. The test was corrected without changing that contract.
The SecurityLab expectation previously treated historical design completion as production approval.
The assertion now checks the current NO-GO approval state.
The GUI account gate returned exit 4 before app launch.
Streaming measurements include retained process memory and cumulative allocation, as documented separately.
Source-copy rollback returned `SOURCE_COPY_RESTORED=PASS` with exit 0.
Actual vault recovery is separately proven by reader exports and matching bytes.

## Actual changed source and workflow files

- `.github/workflows/wo13-windows-validation.yml`
- `src/SmartPerformanceDoctor.App/Services/Security/SecureVaultLabBackend.cs`
- `src/SmartPerformanceDoctor.App/Services/Security/SecureVaultService.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/Hardening/VaultExportWriter.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/Migration/LabToAv3MigrationGate.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/ProductBridge/Av3GateSnapshot.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/ProductBridge/Av3LabEnableChecklist.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/ProductBridge/LabReleaseState.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/Progress/LabRemainingGaps.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabAuthenticatedGeneration.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabPackStore.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabRecoverySlots.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultCrypto.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultJournal.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.Phase2.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.Transactions.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.cs`
- `tests/SmartPerformanceDoctor.SecurityLab.Tests/VaultV4IntegrationTests.cs`
- `tests/SmartPerformanceDoctor.Tests/SecurityAttackSimulationTests.cs`
- `tests/SmartPerformanceDoctor.Tests/WO13VaultTransactionTests.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/CrashSafeProbe.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/Program.cs`

## Results

Read `FULL_TEST_RESULT.txt`, `SECURITYLAB_RESULT.txt`, and `BUILD_RESULT.txt` for final observed totals.
Read `GITHUB_RESULT.txt` for the validated source commit, remote HEAD, tree, and Windows CI.
Read `WO13_PRODUCT_READINESS.md` for remaining P0 and P1 work.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
