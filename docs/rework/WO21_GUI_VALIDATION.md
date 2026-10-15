# WO21 — GUI VALIDATION

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Status

Actual product GUI: NOT TESTED.
Independent SID GUI: NOT TESTED.
UAC secure desktop: NOT TESTED.

GuiProbe built successfully with zero warnings and errors.
The recovery window retains its existing automation identifiers.
The administrator restore button now requires known authenticated current content.
Its guidance explains the second confirmation in the administrator tool.
The normal app routes installation-review states into the existing recovery window.

No screen-control session was started during WO21.
No host profile or real vault was used for GUI testing.
XAML compilation and source identifiers are preparation evidence only.

## Guest procedure

Restore the verified guest checkpoint.
Select the independent standard test SID.
Verify isolated AppData, TEMP and synthetic fixture roots.
Launch the guest test-profile application.
Use GuiProbe against the actual application process.
Open Dashboard, Diagnostics, SystemCare, StorageCare, Vault and Update Recovery.
Exercise keyboard focus, resize, narrow layouts, high DPI and reduced motion.
Test UAC cancellation and the post-elevation target confirmation.
Verify the stale, missing and mismatched installation messages.
Use a screen reader for accessibility claims.
Close the application and end screen control immediately after testing.
Restore the checkpoint and query its actual restoration state.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
