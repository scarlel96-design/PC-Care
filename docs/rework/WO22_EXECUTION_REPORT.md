# WO22 — Execution report

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Implemented results

The implementation separates consumed approval from authenticated restoration completion.
It adds read-only RequestId reconciliation and UnknownContent evidence review.
It preserves incomplete enrollment records for review.
It restores trust only through authenticated original enrollment after verified restoration.
It adds distinct identity, ACL, signature and content errors.
It supports safe preparation cancellation and defers cancellation after mutation.
It reuses pinned targets and pooled hash buffers.
It connects cancellation and recovery state queries to existing product services.
It gives each qualification run a fresh TRX with binary and source receipts.

## Baseline and regression

The clean baseline ran 1289 PASS, 111 SKIP and zero failures.
The same 111 skipped test names remain.
Historical skip text that incorrectly mentioned production GO was corrected.
No existing test was deleted, newly skipped or enabled to inflate results.
Read the final evidence for exact local and CI counts and source SHA.
Updater128 and performance233 remain subsets of the application total.
SecurityLab36 remains separately counted.
An intermediate xUnit2031 analyzer error was corrected before final qualification.

## Recovery evidence

Separate-process termination and full authenticated synthetic v5 extraction are recorded independently.
Source-copy rollback does not restore installation transactions or vault contents.
Actual signed, privileged, UAC, independent-SID GUI and reboot qualification remain OPEN.

## Historical approval wording

Historical full-suite logs retain their original output.
Their old production GO skip text is not current approval evidence.
WO22 corrects all 111 active skip reason strings without changing the skipped names or count.
The current authority remains six false production gates and Phase 1 NO-GO.

## CI recorder failure

Run 38695788701 failed while recording an empty terminated-worker output.
The previous PowerShell Raw cast followed by Trim raised a null-method error.
A separate process reproduced that expression with an empty synthetic file.
The recorder now preserves the exact empty output with File.ReadAllText.
The checkpoint signal and termination code remain mandatory independent evidence.
A dedicated recorder check runs before the process matrix.
No product src, tests or tools changed after implementation 02c140c.

## CI preparation output correction

Run 38699054040 reached the process matrix successfully.
The signing preparation output referenced the absent WO21 output directory.
The workflow now writes into the existing WO22 output directory.
The preparation-only command returned PREPARED in a local output-path check.
This check does not sign a PE file or qualify a signed deployment.

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.

New WO22 tests: 44 PASS, included in app total.
Actual synthetic checks: 295 commands, 29 terminated workers and 272 matching vault SHA-256 pairs.

GitHub Actions emitted runtime deprecation annotations.
These annotations are separate from Restore and Release compiler warnings.
Read `CI_WORKFLOW_WARNINGS.json` for the literal output.
