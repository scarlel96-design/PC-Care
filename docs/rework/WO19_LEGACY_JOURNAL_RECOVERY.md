# WO-19 — Legacy Journal Recovery

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

DiagnoseLegacy remains read-only.
PlanLegacyRecovery binds the original journal hash, directory IDs, and observed target IDs.
It requires an independently signed original-version package.
The package ToVersion must match the legacy FromVersion.
Backup length and SHA-256 must match that authenticated package.
An untrusted package returns RecoveryDenied.
A verified backup is reported as BackupVerified.
The plan records a consent binding hash.
This hash detects plan changes. It is not a signature or mutation capability.

CanExecute remains false.
Actual reviewed administrator restoration is NOT IMPLEMENTED.
No schema 1 record receives automatic overwrite or deletion authority.
Missing, changed, or unverifiable objects require manual review.
The synthetic test verifies the journal hash remains unchanged.
The synthetic test verifies current installation content remains unchanged.

Next: implement a signed-helper execution contract with fresh ID, ACL, and hash checks.
Keep the original legacy journal as immutable evidence.
Test recovery-plan interruption before granting production authority.

Evidence: `docs/rework/evidence/wo19/`

FINAL WINDOWS CI: PASS. Run 38576304293 verified source 347a34fa1f273db7064f40e3c53ecea4fd10ecee.
Full application: 1230 PASS / 111 SKIP / 0 FAIL. SecurityLab: 36 PASS / 0 FAIL.
WO-19: 16 PASS. Existing updater: 128 PASS. Performance: 233 PASS. These subsets overlap the full application total.
Process matrix: 20 apply and 13 recovery terminations. Deletion: 17 terminations. Quarantine: 6 terminations.
Restore and Release: 0 warnings / 0 errors. The dedicated VM and normal product GUI remain NOT TESTED.
CI evidence: CI_FINAL_RESULT.json, CI_JOB_LOG.txt, and CI_*.trx.

Termination accounting: 46 worker kills include 13 setup kills for the 13 recovery interruption points.
The matrix has 20 apply interruption locations and 13 recovery interruption locations.
Two image/DLL lock attempts returned expected exit 5. After lock release, recovery and reapplication passed.
