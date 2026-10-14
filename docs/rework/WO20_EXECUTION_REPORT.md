# WO20 — EXECUTION REPORT

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Implementation outcome

The updater UI now dispatches through the trusted fixed Bridge contract when transition is required.
The administrator legacy restore entry point performs fresh package, ID, ACL, content and consent checks.
Reviewed legacy transactions preserve schema 1 evidence and reuse the current rollback engine.
The application checks update state before product service construction.
Signed committed declarations support current installed-file inspection.
Actual synthetic v5 extraction verifies full bytes around updates and restoration.

## Source changes

The changed-file list is preserved in VERIFICATION.txt and DIFF_FILE.
No cryptographic algorithm or vault format was changed.
The existing TraceEvent, CsWin32, WinUI, .NET crypto and Windows file-boundary implementations were reused.
No dependency version was upgraded.

## Evidence limits

Read FULL_TEST_RESULT.txt and CI evidence for observed totals.
Subset results are included in the app total and are not added again.
Read SecurityLab's separate result independently.
Read the final GitHub receipt for tested-source versus final documentation-commit differences.

Signed first transition, trusted Bridge distribution, actual administrator legacy restore and general GUI remain open.
The partial-byte legacy restore case remains review-required when content cannot be authenticated.
Restart and physical durability evidence remain missing.

## Evidence and reproduction

Read `evidence/wo20/VERIFICATION.txt` for commands, inputs, exits and source provenance.
Read the named JSON evidence for literal process outputs and SHA-256 comparisons.
Use only synthetic fixtures. Keep private fixture passwords and DPAPI files outside published evidence.

## Restore procedure

Use `ROLLBACK.sh` only on an independent source copy named RESTORE_COPY.
Use the trusted updater recovery entry point for authenticated schema 2 transactions.
Review schema 1 plans with the signed original-version package and explicit consent.
Preserve files when identity, content, ACL, journal or package checks fail.
Source rollback does not restore installation data or encrypted vault contents.

## Local synthetic update measurements

These single-run measurements overlapped other local regression processes.
They are not isolated performance benchmarks. KDF is outside each measured apply process.

| Input | Logical bytes | Backup bytes | Package bytes | Apply ms | CPU ms | Peak MiB |
|---|---:|---:|---:|---:|---:|---:|
| prepare | 9 | 13 | 1102 | 1856.51 | 1218.75 | 52.33 |
| prepare-random | 33554438 | 16777226 | 35390348 | 4629.20 | 4203.12 | 55.63 |
| prepare-many | 1048585 | 524301 | 1153792 | 53839.12 | 49234.38 | 181.73 |

The many-small-file case needs separate profiling. No processing improvement is claimed.
Mid-apply cancellation is NOT MEASURED because the apply API has no CancellationToken.
Low-end hardware is NOT TESTED.

## Reused technologies

TraceEvent 3.2.8, CsWin32 0.3.358, TraceProcessing 1.12.10 and Windows App SDK 2.2.0 remain unchanged.
Existing .NET crypto, ECDSA verification, WindowsFileBoundary and rollback APIs are reused.
No new package was adopted.

Microsoft API references: [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0), [UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0), [Directory.EnumerateFiles](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles?view=net-10.0).

## Final local build-path qualification

The solution's x64 configuration maps the app test project to bin/Release.
An x64 --no-build invocation instead uses bin/x64/Release.
That mismatch omitted one newly added test in the earlier local result.
Build the test project directly with -p:Platform=x64 before using --no-build.
Do not rebuild a shared test output directory while testhost holds its assemblies.
The lock failure is preserved as a failed build attempt.
The final focused and full results use a freshly built x64 test project.

Final local outcomes: `{"Passed": 1261, "NotExecuted": 111}`. New WO20 outcomes: `{"Passed": 31}`.

## Final Windows CI

[Windows CI 38641969661](https://github.com/scarlel96-design/PC-Care/actions/runs/38641969661) succeeded for `640d2cd0e9d506a4cb66629592ed7d7296e04456`.
Full app: 1261 PASS, 111 SKIP, zero failures. SecurityLab: 36 PASS, zero failures.
New WO20: 31 PASS. The focused CI group also includes one existing scanner test.
Updater128 and performance233 are subsets of the full app total.
The CI matrix contains83 new process records and197 existing updater process records.
Nine new workers were killed. Thirty-three v5 unlock/extraction checks verified132 complete SHA-256 pairs.
This clean-checkout run does not qualify Authenticode Bridge deployment, UAC, actual product GUI or reboot.
