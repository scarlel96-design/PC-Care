# WO-12 GUI validation

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

Status: **NOT TESTED** for live GUI. Environment preparation: **BLOCKED**.

The host has no verified independent interactive test account for this operation.
The GUI preparation script requires a test SID different from the original user SID.
The script verifies the current SID and the profile path before launch.
It rejects reparse ancestors. It assigns a synthetic vault root beneath the test profile.
GuiProbe can inspect the live vault page using --inspect-vault-page PID.
This probe verifies the window/session and required Automation identifiers.
It does not automate secret entry or certify all usability scenarios.

## Executed gate

Command: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/wo12-gui-validation.ps1`.
Input: no dedicated-account SIDs and no launch request.
Output: `{"status":"BLOCKED","guiExecuted":false,"reason":"Dedicated test account required"}`.
Exit code: 4.
The app was not launched by this command.

## Reproducible procedure

1. Prepare a dedicated standard Windows test account.
2. Sign in through its interactive desktop.
3. Record its SID and the original user's SID privately.
4. Build the production-policy Release app and GuiProbe.
5. Run wo12-gui-validation.ps1 with both SIDs, AppPath, ProbePath and -Launch.
6. Open the vault page and confirm its actual execution policy.
7. Record any trust-policy block without changing the gate.
8. Use an approved isolated test build for synthetic write scenarios.
9. Test create, unlock, import, export, cancel and repeat.
10. Test keyboard focus, narrow windows, high DPI and a screen reader.
11. Compare all exported synthetic file hashes.
12. Record memory, CPU and cancellation response.

Settings isolation uses the independent Windows profile. ACL isolation still requires actual measurement.
The script does not manufacture production trust or authorize an AV3 Writer.
