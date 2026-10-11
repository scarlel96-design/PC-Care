# WO-12 execution report

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

## Outcome

Implemented source preservation, collision-safe handle-based export, cancellation propagation and stable list updates.
Reused WindowsFileBoundary through a Lab namespace adapter.
Stopped unauthenticated journal records from deleting encrypted objects.
Added session ownership, read-only journal preservation, a process crash probe and a dedicated-account GUI harness.
The implementation is PARTIAL because a metadata/commit interruption still blocks automatic recovery.

## Verified local environment

Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors.
Commands ran from the isolated source checkout. Synthetic data used dedicated generated fixture roots.
No user vault was selected. The default GUI account gate did not launch the app.
Network restore and native Windows handle tests required host execution outside the restricted command sandbox.
The first broad run exposed a real reparse-message regression. The production validator was corrected.
The final broad run passed 1,019 cases and retained all 111 baseline skips.
NuGet feed failures on earlier commands were retried after successful restore. They are not test passes.

## Files changed

- `src/SmartPerformanceDoctor.App/Services/Security/SecureVaultLabBackend.cs`
- `src/SmartPerformanceDoctor.App/Services/Security/SecureVaultService.cs`
- `src/SmartPerformanceDoctor.App/Services/Security/VaultImportPathValidator.cs`
- `src/SmartPerformanceDoctor.App/ViewModels/SecureVaultViewModel.cs`
- `src/SmartPerformanceDoctor.App/Views/SecureVaultCenterPage.xaml`
- `src/SmartPerformanceDoctor.App/Views/SecureVaultCenterPage.xaml.cs`
- `src/SmartPerformanceDoctor.App/Views/SecureVaultOperationProgressDialog.cs`
- `src/SmartPerformanceDoctor.SecurityLab/SmartPerformanceDoctor.SecurityLab.csproj`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabFaultMatrix.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabStreamImportMatrix.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultJournal.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.cs`
- `src/SmartPerformanceDoctor.UpdateEngine/WindowsFileBoundary.cs`
- `tests/SmartPerformanceDoctor.SecurityLab.Tests/VaultV4IntegrationTests.cs`
- `tools/SmartPerformanceDoctor.GuiProbe/Program.cs`
- `.github/workflows/wo12-windows-validation.yml`
- `docs/rework/WO12_CRYPTO_SECURITY_REVIEW.md`
- `docs/rework/WO12_DATA_INTEGRITY.md`
- `docs/rework/WO12_FORMAT_COMPATIBILITY.md`
- `docs/rework/WO12_GUI_VALIDATION.md`
- `docs/rework/WO12_OPEN_SOURCE_EVALUATION.md`
- `docs/rework/WO12_PRODUCT_READINESS.md`
- `docs/rework/WO12_VAULT_ARCHITECTURE_AUDIT.md`
- `docs/rework/WO12_VAULT_CRASH_RECOVERY.md`
- `docs/rework/WO12_VAULT_FUNCTIONAL_TESTS.md`
- `docs/rework/WO12_VAULT_UI_UX.md`
- `docs/rework/evidence/wo12/DIFF_FILE.patch`
- `docs/rework/evidence/wo12/MODIFIED_FILE.cs.txt`
- `docs/rework/evidence/wo12/ORIGINAL_FILE.cs.txt`
- `docs/rework/evidence/wo12/ROLLBACK.sh`
- `docs/rework/evidence/wo12/SOURCE_HASHES.json`
- `docs/rework/evidence/wo12/SOURCE_ROLLBACK.ps1`
- `scripts/wo12-gui-validation.ps1`
- `src/SmartPerformanceDoctor.SecurityLab/Properties/AssemblyInfo.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/Hardening/VaultExportWriter.cs`
- `tests/SmartPerformanceDoctor.Tests/WO12VaultReliabilityTests.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/Program.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/SmartPerformanceDoctor.VaultProbe.csproj`

## Evidence and source identity

Read evidence/wo12/VERIFICATION.txt for exact commands, results and source-copy rollback.
Read evidence/wo12/GITHUB_RESULT.txt for tested source, remote HEAD and CI provenance.
The final remote evidence commit can differ from the tested source commit.
Its additional changes must be limited to evidence documents.
Use `git rev-parse HEAD^{tree}` to identify the final tree independently.
The final tree cannot be stored inside itself without changing that tree.

## Residual risks

| Priority | Unresolved condition | Required next evidence |
|---|---|---|
| P0 | MetadataReady termination preserves ciphertext but blocks unlock | Authenticated versioned metadata and commit recovery; separate-process byte export |
| P0 | Password-change header/commit writes lack a tested atomic protocol | Kill each write boundary; verify old or new password and all bytes |
| P0 | Creation and same-instance operations lack a complete transaction mutex | Concurrent create/import/export/lock failure tests |
| P0 | Journal append errors are swallowed; malformed lines are ignored | Fail-visible durable journal and corruption tests |
| P0 | Explicit legacy source-sealing remains a path-based destructive path | Handle-bound ownership and deletion; keep default off |
| P1 | Recovery code can be consumed before session lease acquisition | Duplicate-session recovery-code test and atomic consumption |
| P1 | Export decrypts whole files into memory | Streaming authenticated export and bounded memory measurement |
| P1 | Independent GUI account is absent | Dedicated interactive account, isolated settings, UIA and accessibility results |
| P1 | Custom HChaCha20 has no new independent vector review in WO-12 | Published vectors and maintained-library compatibility study |
| P1 | Read-only unlock still changes audit and rate-limit files | Distinguish operational read-only from immutable forensic snapshots |
