# WO-19 — Gui Validation

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

LIVE RESULT: PARTIAL.
The local interactive Windows desktop was available.
SmartPerformanceDoctor.exe --readonly-update-status opened the actual WinUI window.
The window displayed the Korean NoPendingTransaction state.
The refresh button was clicked.
Tab moved visible focus to Close.
Return closed the window and exited the process.
The captured application screenshot is GUI_READONLY_STATUS.jpg.

The screen tool returned a null accessibility tree.
Screen-reader support is NOT TESTED.
High DPI, narrow window, normal dashboard, and the five product pages are NOT TESTED.
The read-only entry point does not construct product services or deferred startup tasks.
It does not unlock runtime trust or expose a recovery mutation button.
The GUI trial used source 1fa021ef142e6589f5baf040744a462dc85437d1.
The final source adds strict ACL inspection. That change was not re-exercised through GUI control.
The screen-control session was reset immediately after the trial, as requested.

Evidence: `docs/rework/evidence/wo19/`

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
