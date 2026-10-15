# WO22 — Installation trust consistency

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Trust states

The existing policy distinguishes fresh, legacy unverified, signed verified, mismatched, pending, recovery and migration-review states.
An unsigned legacy installation remains unverified. It is not automatically classified as malicious.
A missing or corrupt declaration never becomes Verified.

## Enrollment durability

Enrollment writes a unique prepared record with CreateNew.
It Flushes the record and revalidates directories, package, declared files and security.
It then activates the fixed enrollment record with a single-file move.
Incomplete prepared records require migration review.
They are not activated automatically.
Multiple-file changes are not described as one atomic filesystem write.

## Restoration consistency

A trusted restored transaction can invalidate the current update declaration for the restored bytes.
The policy then checks the original signed enrollment through the normal identity, content and ACL requirements.
It returns Verified only when those retained original records authenticate the restored installation.
A corrupt current pointer does not trigger this fallback.
A later verified signed declaration supersedes restored history only for its declared paths.
Other historical paths retain their restoration checks.
Extra undeclared files are not authenticated by a declaration that excludes them.

## Result codes

`INSTALLATION-NOT-ATTESTED` denotes missing authentication evidence.
Content, identity, ACL and signature failures have separate explicit codes.
Recovery and manual-review states retain distinct codes.
Existing content-mismatch callers retain compatible behavior.
The source-copy experiment proves the restored-enrollment branch before, after and after rollback.
It is separate from actual installation transaction recovery.

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.
