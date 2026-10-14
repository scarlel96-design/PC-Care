# WO20 — STARTUP ATTESTATION

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Earlier inspection

The App constructor inspects update recovery before product host and background services are constructed.
Pending or untrusted installation state routes to the limited recovery window.
OnLaunched inspects again before the normal main window opens.
The cached early state prevents opening product features after their construction was skipped.

## Signed installation record

A committed update retains its signed manifest and signature.
The protected current pointer binds transaction ID, transaction directory identity and installation identity.
It also binds the retained attestation SHA-256.
Inspection validates the signature and every declared installed file hash, length and identity.
Restored current transactions can inspect the previous pointer.
Missing attestation returns NotAttested, never Verified.
Malformed or mismatched records require review and do not authorize repairs.
Historical manifest verification grants no permission to apply an expired package.

## Boundary

Managed assemblies and WinUI initialization can occur before the constructor guard.
This implementation is not an independent native pre-execution verifier.
The manifest covers the signed package list, not every unrelated installation file.
Local administrator or full-machine replay remains outside the demonstrated freshness boundary.
A minimal signed external launcher remains a product P0 follow-up.

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
