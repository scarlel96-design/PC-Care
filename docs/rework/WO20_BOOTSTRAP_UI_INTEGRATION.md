# WO20 — BOOTSTRAP UI INTEGRATION

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Implemented flow

UpdateInstallerService inspects the signed package and the installed helper protocol.
BootstrapDispatch selects the existing helper or the fixed external UpdateBridge.
The fixed Bridge root is ProgramFiles/PC-Care UpdateBridge.
No user-supplied executable path or environment override selects the elevated executable.
UpdateStatusViewModel reports the actual launch result and exits only after a process starts.
TransitionLaunched means installation verification remains pending.

## State contract

The enum defines BootstrapNotRequired, BootstrapTransitionRequired, BridgeUnavailable and BridgeUntrusted.
It also defines SignedPackageRejected, TransitionPrepared, AwaitingUserApproval and ElevationCancelled.
TransitionLaunched, TransitionInterrupted, RecoveryRequired and TransitionVerified complete the contract.
Some states are preparation contracts rather than independently observed GUI transitions.
Synthetic boolean route tests do not replace actual signature verification.

## Remaining validation

The production publisher allowlist remains empty.
The trusted Bridge distribution and first signed transition remain P0 OPEN.
No installed product was changed. Actual update GUI and UAC interaction are NOT TESTED.

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
