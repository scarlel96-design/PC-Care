# WO21 — PARTIAL CONTENT RECOVERY

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Content classification

OriginalVerified requires the independently signed old package hash and length.
UpdatedVerified requires a retained independently signed current declaration and matching bytes.
The untrusted legacy journal's updated hash alone does not establish UpdatedVerified.
A missing or invalid signed declaration produces UnknownContent.
A truncated or unrelated file remains UnknownContent unless independent evidence identifies it.
A short length alone does not prove a partial write.
BackupVerified requires the independently signed original content, length and actual backup identity.

## Mutation policy

An old approved plan cannot overwrite changed current bytes.
A freshly generated plan also cannot authorize UnknownContent.
The synthetic unknown-content scenario verifies the same SHA-256 before and after the blocked request.
The authenticated backup is preserved for manual recovery review.
No source bytes are reconstructed without authenticated evidence.
No automatic quarantine or replacement of uncertain current data was added.
This conservative case remains RecoveryReviewRequired.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
