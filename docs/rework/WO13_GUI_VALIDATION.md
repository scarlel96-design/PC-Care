# WO-13 GUI validation

Status: **NOT TESTED**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


Command: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/wo12-gui-validation.ps1`.
Exit code: 4.
Literal output: `{"status":"BLOCKED","guiExecuted":false,"reason":"Dedicated test account required"}`.
The account gate stopped execution before launching the product GUI.
Headless tests and XAML compilation do not provide GUI PASS.
Existing recovery state messages distinguish preserved data, read-only recovery, and required review.

## Prepared execution procedure

1. Create a dedicated disposable Windows test account.
2. Prepare synthetic fixtures outside the normal user's profile.
3. Read the existing harness parameters before starting GuiProbe.
4. Supply the dedicated account and isolated data root.
5. Launch the test application through the validated harness.
6. Test unlock, import, export, cancellation, and duplicate-work rejection.
7. Check recovery messages, keyboard focus, screenreader labels, DPI, and narrow-window layout.
8. Record actual UI outcomes and memory samples.

The v6 fixture format is not exposed as an approved product Writer button.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
