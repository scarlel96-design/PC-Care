# WO-19 — Bootstrap Transition

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

IMPLEMENTATION: UpdateBridge runs outside the installed helper directory.
The bridge requires administrator execution and its own compiled Authenticode publisher policy.
PrepareTransition validates the install root and the signed update package.
It copies the package into the protected bootstrap directory.
It extracts the five declared helper runtime files.
It verifies each runtime file against the signed manifest.
It verifies executable and DLL Authenticode publishers.
It binds the installation ID and bootstrap ID in the existing binding record.
The snapshot helper waits for the bridge and caller processes.
It then uses the existing transaction apply and recovery engine.

The helper declares PC-Care.UpdateBootstrapProtocol=1 in assembly metadata.
PEReader reads the declaration without loading the assembly.
LaunchInstalled rejects a helper without that declaration.
This declaration does not substitute for publisher verification.

ACTUAL RESULT: bridge build PASS; metadata detection PASS; production trust remains empty.
Signed first-transition E2E is NOT TESTED.
The ordinary update UI does not yet dispatch the external bridge automatically.
The bridge distribution and signed test-profile E2E remain P0 work.
The existing synthetic image-lock test is not a signed bootstrap-transition test.

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
