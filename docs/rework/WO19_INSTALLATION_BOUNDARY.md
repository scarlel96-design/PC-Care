# WO-19 — Installation Boundary

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

WindowsFileBoundary remains the shared handle-based boundary.
DirectoryPin retains local fixed-disk ancestor handles.
The boundary rejects reparse points and multiple hardlinks.
FileLease checks file ID, volume ID, and final handle path.
Publication retains the source and destination ancestor pins.
The Windows rename request does not replace an existing target.
Collision tests preserve the existing target bytes.
Extended paths support staging paths longer than 260 characters.

Startup now validates protected ACLs during read-only inspection.
The elevated helper retains strict owner and ACL checks during mutation.
No UAC prompt was accepted or rejected in this local task.
No installed product ACL was changed.
Actual isolated standard-SID installation ACL validation is NOT TESTED.
Windows restart and VM power-off are NOT TESTED.
The install-root policy still relies on Program Files location, IDs, and protected ACLs.
Registered-installation attestation needs further review.

Evidence: `docs/rework/evidence/wo19/`

Official references: [FILE_RENAME_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info), [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle).

ENVIRONMENT DISCOVERY: Get-VM, vmrun, and VBoxManage were not found. The vmms service was not found.
No disposable snapshot or independent test SID was verified. This discovery is not a complete host VM inventory.
Evidence: VM_ENVIRONMENT_RESULT.json.

FINAL WINDOWS CI: PASS. Run 38576304293 verified source 347a34fa1f273db7064f40e3c53ecea4fd10ecee.
Full application: 1230 PASS / 111 SKIP / 0 FAIL. SecurityLab: 36 PASS / 0 FAIL.
WO-19: 16 PASS. Existing updater: 128 PASS. Performance: 233 PASS. These subsets overlap the full application total.
Process matrix: 20 apply and 13 recovery terminations. Deletion: 17 terminations. Quarantine: 6 terminations.
Restore and Release: 0 warnings / 0 errors. The dedicated VM and normal product GUI remain NOT TESTED.
CI evidence: CI_FINAL_RESULT.json, CI_JOB_LOG.txt, and CI_*.trx.

Termination accounting: 46 worker kills include 13 setup kills for the 13 recovery interruption points.
The matrix has 20 apply interruption locations and 13 recovery interruption locations.
Two image/DLL lock attempts returned expected exit 5. After lock release, recovery and reapplication passed.
