# WO-19 — Update Architecture

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

The transaction engine retains signed v2 manifests, file handles, backups, and schema 2 journals.
New files now enter the protected transaction directory before installation publication.
The source file ID remains bound during publication.
The destination parent remains pinned after publication.
Existing files still use verified backups and handle-bound writes.
This process does not make multiple file changes atomic.

Recovery inspects all trusted journals before restoring a file.
Multiple pending mutations return MULTIPLE-PENDING-TRANSACTIONS without changing files.
Startup inspects pending transactions before constructing MainWindow.
Pending, locked, failed, or rejected transactions open UpdateRecoveryWindow.
Startup inspection now checks the protected directory owner and ACL.
It does not create a directory, acquire a write lock, or restore files.
The recovery request still uses the authenticated elevated helper.
The ordinary shell does not open during pending recovery.

Residual: startup does not attest every historical committed installation file.
Residual: constructors and assembly loading occur before OnLaunched.

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
