# WO20 — LEGACY RESTORE EXECUTOR

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Authorization

LegacyRecoveryPlan remains read-only with CanExecute=false.
The plan now binds current length, current security descriptor and original package file identity.
Its digest includes journal hash, installation identity, transaction identity and every selected target.
ConsentBinding is a digest, not a signed authorization token.

ExecuteLegacyRecovery requires consent and recomputes the approved digest.
Strict production execution requires administrator membership and protected installation/work ACLs.
It regenerates the plan from the signed original-version package.
It compares package hash, file ID, version, journal hash, directory identities and target observations.
It blocks ambiguous concurrent transactions and absent legacy targets.
It pins backups and targets before promotion and verifies bytes, IDs, lengths and security descriptors.
It preserves the original schema 1 record under a hash-derived evidence name.
It promotes only a reviewed transaction to schema 2 and reuses RollbackCore.

## Product connection

The recovery window provides package review, explicit consent and an administrator execution request.
Execution remains disabled unless the separate Bridge passes production trust checks.
The Bridge accepts --legacy-restore, rechecks its own trust and validates the request before engine execution.
Request JSON does not grant authority. The engine performs fresh checks.

## Limits

Synthetic engine execution uses strictAcl=false and a fixture signing key.
It proves actual file restoration, not production administrator deployment.
Unknown partially restored bytes remain review-required when their hash is not recognized.
No backup, uncertain file or original legacy journal is deleted by this authorization step.

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
