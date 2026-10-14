# WO-19 — Execution Report

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

IMPLEMENTED:
1. Prepare new files outside the installation.
2. Bind expected length and file ID before publication.
3. Preserve unidentified staging residue.
4. Inspect every pending transaction before restoration. Block ambiguous multiple pending mutations.
5. Read helper protocol metadata without loading the helper.
6. Add the external signed transition bridge.
7. Bind read-only legacy plans to signed original-package evidence.
8. Gate the normal shell before pending recovery.
9. Remove version-only pending success.
10. Verify protected ACLs during startup inspection.
11. Strengthen process-crash and whole-installation hash checks.

Baseline local full run: 1213 PASS / 1 FAIL / 111 SKIP.
Failure: CareFolderScanner_TempPath_CompletesQuickly timed out.
That failed run remains evidence. It is not reclassified as PASS.
The formal prior baseline is WO-18 Windows CI, source d3467ea91ad916ff405705939f0c62c65e70153f.
Focused updater final run: 144 PASS / 0 FAIL / 0 SKIP.
New WO-19 tests: 16, included in that updater count.
SecurityLab local regression: 36 PASS / 0 FAIL / 0 SKIP.
Local solution Release build: 0 warnings / 0 errors.
Local full run at source 75844a9018216fe4f0b53187592241c43462b94d: 1230 PASS / 0 FAIL / 111 SKIP.
Final source 347a34fa1f273db7064f40e3c53ecea4fd10ecee: updater 144 PASS. Windows CI 38576304293 succeeded for that exact source.
A prior full run without RUNNER_TEMP failed 45 tests. The missing isolated fixture parent triggered protection gates.
The failed run remains evidence. The explicit fixture environment passed the full retry.

SOURCE ROLLBACK: independent source-file copy returned to the original SHA-256.
This does not restore encrypted vault data or overwritten installed content.
ACTUAL TRANSACTION ROLLBACK: the process matrix verified original synthetic installation file hashes.

Evidence: `docs/rework/evidence/wo19/`

FINAL WINDOWS CI: PASS. Run 38576304293 verified source 347a34fa1f273db7064f40e3c53ecea4fd10ecee.
Full application: 1230 PASS / 111 SKIP / 0 FAIL. SecurityLab: 36 PASS / 0 FAIL.
WO-19: 16 PASS. Existing updater: 128 PASS. Performance: 233 PASS. These subsets overlap the full application total.
Process matrix: 20 apply and 13 recovery terminations. Deletion: 17 terminations. Quarantine: 6 terminations.
Restore and Release: 0 warnings / 0 errors. The dedicated VM and normal product GUI remain NOT TESTED.
CI evidence: CI_FINAL_RESULT.json, CI_JOB_LOG.txt, and CI_*.trx.

CHANGED CODE, TEST, AND WORKFLOW FILES:
- .github/workflows/wo19-windows-validation.yml
- scripts/wo06-process-validation.ps1
- scripts/wo19-source-contract.ps1
- src/SmartPerformanceDoctor.App/App.xaml.cs
- src/SmartPerformanceDoctor.App/Services/AppLaunchOptions.cs
- src/SmartPerformanceDoctor.App/Services/Update/UpdatePendingApplier.cs
- src/SmartPerformanceDoctor.App/Services/Update/UpdateRecoveryCoordinator.cs
- src/SmartPerformanceDoctor.App/UpdateRecoveryWindow.cs
- src/SmartPerformanceDoctor.UpdateEngine/LegacyRecoveryPlan.cs
- src/SmartPerformanceDoctor.UpdateEngine/UpdateBootstrap.cs
- src/SmartPerformanceDoctor.UpdateEngine/UpdateTransaction.cs
- src/SmartPerformanceDoctor.UpdateEngine/WindowsFileBoundary.cs
- src/SmartPerformanceDoctor.UpdateHelper/BootstrapProtocol.cs
- tests/SmartPerformanceDoctor.Tests/SignedUpdateTests.cs
- tests/SmartPerformanceDoctor.Tests/WO06RecoveryTests.cs
- tests/SmartPerformanceDoctor.Tests/WO19UpdateRecoveryTests.cs
- tests/SmartPerformanceDoctor.UpdateRecoveryProbe/Program.cs
- tools/SmartPerformanceDoctor.UpdateBridge/Program.cs
- tools/SmartPerformanceDoctor.UpdateBridge/SmartPerformanceDoctor.UpdateBridge.csproj

Termination accounting: 46 worker kills include 13 setup kills for the 13 recovery interruption points.
The matrix has 20 apply interruption locations and 13 recovery interruption locations.
Two image/DLL lock attempts returned expected exit 5. After lock release, recovery and reapplication passed.
