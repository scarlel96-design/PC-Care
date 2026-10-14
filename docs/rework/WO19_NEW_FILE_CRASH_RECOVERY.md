# WO-19 — New File Crash Recovery

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

ROOT CAUSE: the old temporary file lived inside the installation before its file ID reached the journal.
The AfterNewFileCreated interruption left an unidentified installation file.
The old engine preserved that file and required review.

IMPLEMENTATION: TemporaryInTransaction records the preparation location.
NewLength records the signed expected length.
The temporary file resides under tx-ID/newfiles.
The journal records intent before creation.
The engine records the opened file ID before content writes.
The engine verifies length and the full SHA-256 before publication.
Publication uses SetFileInformationByHandle without replacement permission.
Recovery preserves an unidentified staging orphan.
The orphan never grants installation deletion authority.
Legacy installation-side temporary files retain their stricter review rule.

Additional failure: the first process run hit the legacy Windows path length boundary.
The required crash checkpoint was not reached.
Extended Win32 paths corrected the file-open failure.
The harness now requires exit -1 and checkpoint output for every crash.
An ordinary error exit is no longer accepted as a forced termination.

SAME VOLUME: publication rejects different drive roots.
Cross-volume installations remain unsupported by this publication path.
Physical power-loss durability is NOT TESTED.

Evidence: `docs/rework/evidence/wo19/`

Official references: [FILE_RENAME_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info), [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle).

FINAL WINDOWS CI: PASS. Run 38576304293 verified source 347a34fa1f273db7064f40e3c53ecea4fd10ecee.
Full application: 1230 PASS / 111 SKIP / 0 FAIL. SecurityLab: 36 PASS / 0 FAIL.
WO-19: 16 PASS. Existing updater: 128 PASS. Performance: 233 PASS. These subsets overlap the full application total.
Process matrix: 20 apply and 13 recovery terminations. Deletion: 17 terminations. Quarantine: 6 terminations.
Restore and Release: 0 warnings / 0 errors. The dedicated VM and normal product GUI remain NOT TESTED.
CI evidence: CI_FINAL_RESULT.json, CI_JOB_LOG.txt, and CI_*.trx.

Termination accounting: 46 worker kills include 13 setup kills for the 13 recovery interruption points.
The matrix has 20 apply interruption locations and 13 recovery interruption locations.
Two image/DLL lock attempts returned expected exit 5. After lock release, recovery and reapplication passed.
