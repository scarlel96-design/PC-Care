# WO20 — GUI VALIDATION

Status: NOT TESTED. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Prepared assets

The existing GuiProbe now recognizes --inspect-update-page.
It locates UpdateRecoveryStatus, UpdateRecoveryRefresh and UpdateRecoveryClose.
It invokes only the refresh control and reports partial accessibility observations.
LegacyRecoveryReview and LegacyRecoveryExecute have explicit automation identifiers.
The source contract verifies code wiring and prints LIVE_GUI=NOT_TESTED.
The actual GuiProbe build result is recorded separately.

## Environment

Get-VM, vmrun, VBoxManage and the vmms service were absent in the inspected host environment.
This inventory does not prove that no remote hypervisor exists.
No verified disposable snapshot or independent Windows SID was supplied.
Screen control was not reconnected for this work order.
No actual GUI click, UAC response, screenreader, DPI or keyboard flow was executed in WO20.
WO19's read-only status-window observation is not a complete WO20 product GUI test.

## Runbook

Verify a disposable snapshot in the hypervisor.
Create a dedicated standard test account and separate AppData, TEMP and synthetic vault directories.
Verify SID, owner, ACL and package hashes before launching the isolated test application.
Build the trusted test profile without registering certificates in the user's global trust store.
Run GuiProbe with the explicit test process ID.
Record each interaction and result. Restore the snapshot after each destructive scenario.
Disable screen control immediately after the GUI test.

## Evidence and reproduction

Read `evidence/wo20/VERIFICATION.txt` for commands, inputs, exits and source provenance.
Read the named JSON evidence for literal process outputs and SHA-256 comparisons.
Use only synthetic fixtures. Keep private fixture passwords and DPAPI files outside published evidence.

## Restore procedure

Use `ROLLBACK.sh` only on an independent source copy named RESTORE_COPY.
Use the trusted updater recovery entry point for authenticated schema 2 transactions.
Review schema 1 plans with the signed original-version package and explicit consent.
Preserve files when identity, content, ACL, journal or package checks fail.
Source rollback does not restore installation data or encrypted vault contents.
