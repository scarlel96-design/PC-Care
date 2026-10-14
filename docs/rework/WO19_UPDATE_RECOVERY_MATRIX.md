# WO-19 — Update Recovery Matrix

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

The process harness runs from outside the synthetic installation.
Each apply interruption uses real copied executable and DLL files.
The harness hashes every original installation file before interruption.
It compares every original hash after recovery.
It then reapplies the same signed package and verifies all declared payload hashes.
The synthetic vault marker is not an encrypted vault container.
Actual vault format coverage belongs to separate SecurityLab and vault regression results.

Observed commands: 197.
Apply forced terminations: 20.
Recovery forced terminations: 13.
Unexpected required-zero exits: 0.
Every forced termination used exit -1 and checkpoint output.
The matrix also tests installed-image and DLL-handle locks.
These results do not prove physical power-loss durability.

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
