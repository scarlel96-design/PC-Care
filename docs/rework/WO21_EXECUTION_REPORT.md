# WO21 — EXECUTION REPORT

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Implemented results

WO21 adds durable single-use legacy approvals and exact request-file binding.
It adds signed legacy-content classification and blocks unknown current bytes.
It adds installation trust states and signed, non-destructive enrollment.
It adds a separate fail-closed managed launcher prototype.
It adds source/binary qualification receipts and serialized build/test orchestration.
It reuses encrypted v5 fixtures for recovery-code and update compatibility checks.
It adds VM and test-signing preparation tools without changing this host.
It profiles the small-file update path and reduces serialized journal size.

## Baseline and local checks

The clean WO20 baseline reran with 1261 PASS, 111 SKIP and zero failures.
The initial WO21 focused result was 53 PASS.
The expanded focused result was 57 PASS, including 26 new WO21 cases.
The final local and CI application results are 1289 PASS, 111 SKIP and zero failures.
The final WO21 subset contains 28 passing cases.
SecurityLab records 36 PASS, zero failures and zero skips.
Restore and Release record zero warnings and errors.
Windows CI38669715643 succeeded on source930ed7907daaa95cf15040d130c580248e64bf61.
Subset counts must not be added to the application total.
SecurityLab remains a separate suite.

## Observed correction

One extended synthetic run stopped when its random fixture password violated the existing keyboard-walk policy.
The failed input was kept private. The diagnosis records policy issues without the password.
The generator now validates against the unchanged product policy before vault creation.
The complete 134-command rerun passed.

## Source rollback

The independent source-copy experiment compiled the original, modified and restored attestation implementation.
Original and restored behavior returned INSTALLATION-REVIEW-REQUIRED.
Modified behavior returned INSTALLATION-CONTENT-MISMATCH for the same signed-fixture mutation.
ROLLBACK.sh restored only RESTORE_COPY. MODIFIED_FILE remained changed.
This experiment does not restore installation files or recover encrypted vault data.
Actual installation restoration is documented separately in the process matrix.

## Final ACL correction

Strict inspection now checks installation-directory and declared-file protected ACLs.
The launcher checks its own directory and binary ACLs before trusting its managed results.
The final CI includes the two added ACL regressions.
The earlier CI38668909543 was cancelled before this correction.

## Skip evidence

All111 skipped test names match the baseline.
Some historical reason strings mention production GO.
Those strings do not authorize AV3 production use.
All six current approval flags remain false.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
