# WO21 — INSTALLATION TRUST POLICY

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## States

InstallationTrustState separates FreshInstallation and LegacyUnattestedInstallation.
Neither state is Verified. A missing historical record alone does not classify malware.
SignedInstallationVerified requires a signed declaration and current matching file objects.
SignedInstallationMismatch identifies a declaration-bound content mismatch.
PendingTransaction and RecoveryRequired precede ordinary installation acceptance.
AttestationMissing identifies missing evidence when transaction history remains.
AttestationCorrupted and MigrationReviewRequired remain fail-visible review states.

## Enrollment

EnrollInstallation verifies the signed original-version package and every declared installed file.
It retains file handles, volume/file identities, lengths and complete hashes during enrollment.
Production enrollment requires administrator rights and protected ACLs.
The update lock serializes enrollment with transaction mutation.
CreateNew preserves any existing enrollment record.
The enrollment stores the signed declaration and actual identities without rewriting installed payload files.
The Bridge provides an explicit --enroll entry with elevated console confirmation.
No automatic legacy migration or production registration was enabled.

An enrollment record is not an independent freshness witness.
Protected records share the existing administrator trust boundary.
The policy covers declared files. It does not prove every possible dynamic module is declared.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
