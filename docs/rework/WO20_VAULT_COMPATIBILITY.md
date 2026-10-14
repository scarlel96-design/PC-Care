# WO20 — VAULT COMPATIBILITY

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Real encrypted fixtures

The probe creates SecurityLab v5 synthetic encrypted vaults.
Each vault contains four inputs: 0 bytes, 4 KiB, 1 MiB and 4 MiB.
Original hashes are captured before ImportBytes clears its caller buffer.
Each check starts a separate process, unlocks the vault and performs authenticated extraction.
Every extracted file is compared with the original whole-file SHA-256.

The harness checks vaults before and after normal updates, update interruption and actual restoration.
It checks metadata, object references and the unlock path through existing SecurityLab APIs.
File existence alone is never counted as encrypted data recovery.
The JSON evidence preserves all original and extracted hash pairs.

## Limits

Reboot-specific access is NOT TESTED.
WO20 update-specific v4 and v6 compatibility is NOT TESTED.
Existing format regressions remain separate tests.
The dangerous public v4/v5 password change path remains blocked.
AV3 writer, journal writer and migration approvals remain false.
Initial fixture failures are preserved, including password policy and expected-hash timing errors.
Neither failure is relabeled as a product data-loss defect or a successful run.

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
