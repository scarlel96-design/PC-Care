# WO20 — ACL UAC REBOOT

Status: NOT TESTED. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Verified and unverified boundaries

Synthetic Windows file identity and byte checks execute in isolated fixture directories.
Those engine tests explicitly set strictAcl=false.
They do not establish ProgramFiles or protected ProgramData deployment ACL correctness.
Production code retains protected owner/DACL and administrator checks.
No installed product ACL or Windows security setting was changed.

Standard user deployment, administrator deployment, UAC approval and UAC refusal are NOT TESTED.
Installed helper locks are covered only by synthetic process and file-handle fixtures.
Windows restart, shutdown interruption, VM power-off and physical power failure are NOT TESTED.
No VM snapshot was inferred from a script parameter.

## Required environment

Provide a disposable Windows VM with a verified restorable snapshot.
Use a separate SID and protected installation and transaction directories.
Provide an isolated trusted test signing profile.
Record ACLs, IDs and complete hashes before and after each approved action.
Record approval refusal as cancellation, not successful installation.

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
