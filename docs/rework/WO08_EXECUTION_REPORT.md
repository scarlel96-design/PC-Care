# WO-08 execution report

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

The implementation reuses WO-07 SystemSnapshotProbe, CounterProbe, Normalizer, Statistics, and result contracts.
It reuses CareFindingQualityGate and the existing diagnostics page.
It preserves TraceEvent and CsWin32 versions.
It adds loss accounting, continuous aggregation, bounded profiles, shared lifetimes, and CPU/memory priority selection.
It adds wildcard PDH disk collection, reference contracts, bounded workloads, and evidence reporting.
Existing security, vault, updater, recovery, and feature-gate source files were not changed.
Baseline: 805 PASS, 0 FAIL, 111 SKIP, 916 total.
Modified app: 848 PASS, 0 FAIL, 111 SKIP, 959 total.
Performance subset: 79 PASS, 0 FAIL, 0 SKIP. The app total includes this subset.
SecurityLab: 36 PASS, 0 FAIL, 0 SKIP. SecurityLab uses a separate project.
Release build: zero warnings and zero errors.
The first performance run had one session-conflict failure. Exclusive collection scheduling resolved it.
The first restricted testhost connection failed. Unrestricted retries passed.
The first rollback Bash process failed under restricted Windows object permissions.
Its unrestricted retry exposed PowerShell module resolution. A .NET hash implementation resolved that issue.
The final executable rollback restored an independent source fixture and original behavior.
This source restoration does not validate an installation transaction rollback.

## Information unavailable from GitHub alone
The actual workspace path is recorded in VERIFICATION.txt.
The user installation and real vault were not modified or opened.
No disposable Windows VM was supplied. No reboot, UAC, or physical power-loss test occurred.
The physical host was not reserved for idle measurements.
Sandbox escalation removed process restrictions. It did not add OS administrator membership.
PDH failed under the restricted token and succeeded in the unrestricted nonadmin process.
Kernel ETW stayed unavailable locally. CI uses a distinct Windows-hosted environment.
Native GUI interaction was not executed. Compilation does not prove interactive usability.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.

## Changed files

- `.gitattributes`
- `.github/workflows/wo08-windows-validation.yml`
- `docs/rework/evidence/wo08/DIFF_FILE.patch`
- `docs/rework/evidence/wo08/MODIFIED_FILE.cs`
- `docs/rework/evidence/wo08/ORIGINAL_FILE.cs`
- `docs/rework/evidence/wo08/ROLLBACK.ps1`
- `docs/rework/evidence/wo08/ROLLBACK.sh`
- `scripts/wo08-measurement.ps1`
- `src/SmartPerformanceDoctor.App/Services/SystemCare/PerformanceFindingAdapter.cs`
- `src/SmartPerformanceDoctor.App/Views/PerformanceDiagnosticsPage.xaml`
- `src/SmartPerformanceDoctor.App/Views/PerformanceDiagnosticsPage.xaml.cs`
- `src/SmartPerformanceDoctor.Diagnostics/BottleneckAnalyzer.cs`
- `src/SmartPerformanceDoctor.Diagnostics/Contracts.cs`
- `src/SmartPerformanceDoctor.Diagnostics/CounterProbe.cs`
- `src/SmartPerformanceDoctor.Diagnostics/EtwCollector.cs`
- `src/SmartPerformanceDoctor.Diagnostics/EventBuffer.cs`
- `src/SmartPerformanceDoctor.Diagnostics/NativeMethods.txt`
- `src/SmartPerformanceDoctor.Diagnostics/PerDiskNormalizer.cs`
- `src/SmartPerformanceDoctor.Diagnostics/PerformanceCollector.cs`
- `src/SmartPerformanceDoctor.Diagnostics/ProcessIdentityRegistry.cs`
- `src/SmartPerformanceDoctor.Diagnostics/ReferenceComparison.cs`
- `src/SmartPerformanceDoctor.Diagnostics/WorkloadPolicy.cs`
- `tests/SmartPerformanceDoctor.Tests/WO08AccuracyTests.cs`
- `tools/SmartPerformanceDoctor.DiagnosticsProbe/ControlledWorkload.cs`
- `tools/SmartPerformanceDoctor.DiagnosticsProbe/Program.cs`

## Final Windows CI evidence

Run 38084989987 completed successfully on source commit `77e19b01f1baa579811bbd122b062abaaa460ba1`.
The runner used Windows build 26100, four logical processors, and an elevated token.
The full app passed 848 tests, failed zero tests, and skipped 111 tests.
The performance subset passed 79 tests. The updater subset passed 128 tests.
SecurityLab passed 36 tests in its separate project.
The Release build reported zero warnings and zero errors.
The measurement harness completed with exit code zero and `WO08_MEASUREMENT_COMPLETED`.
All 13 saved collector captures had verified cleanup and zero remaining owned sessions.
Native loss counters were queried before stop. The terminal interval remains unknown.
General mode omits CPU profile and context-switch event streams.
LowOverhead mode tracks lifetimes without resource events. Its zero resource count is expected.
Detailed mode dropped 2,106 queue events and disabled ETW after loss.
Detailed mode recorded 10,323 unattributed events. Attribution completeness is not established.
Independent PDH and system snapshots continued after ETW scope reduction.
Short CPU and mixed captures had fewer than eight samples. They cannot prove persistent saturation.
These different profiles and workloads do not establish a WO-07 improvement percentage.
The measurements do not establish physical low-end hardware performance.

| Profile | Received | Native loss before stop | Queue drop | Malformed | Accepted / processed / pending | CPU % machine | Peak MiB | Range reduced |
|---|---:|---:|---:|---:|---|---:|---:|---|
| Detailed | 16007 | 0 | 2106 | 0 | 13901 / 13901 / 0 | 1.275 | 61.785 | True |
| General | 2692 | 0 | 0 | 0 | 2692 / 2692 / 0 | 1.607 | 54.035 | False |
| LowOverhead | 0 | 0 | 0 | 0 | 0 / 0 / 0 | 1.142 | 52.680 | False |

## Executed independent reference capture

WPR started and stopped successfully on the isolated CI runner. Both commands returned zero.
The trace contained 300,941,312 bytes before removal.
Its SHA-256 was `0461B00ED7E0628F88166FF92CD1A2200FEC3819ABAF4BE763F8E61AB8F06A31`.
WPR reported: `This trace has dropped 39336 events. Please record this trace again.`
The generated ETL was removed after hashing. No raw ETL was uploaded.
The WPR capture overlaps the recorded bounded CPU workload.
No independent WPA interpretation or aligned category ground truth was completed.
Reference comparison remains PARTIAL. Real diagnostic accuracy remains NOT TESTED.
False positives, false negatives, Unknown counts, precision, recall, and contribution errors remain null for independent accuracy.
WPR loss belongs to its own session. It is not PC-Care queue loss.
Use a dedicated runner and a loss-checked reference configuration for the next comparison.
Save an explicitly authorized trace until independent interpretation completes.
The current hash proves trace creation. It does not permit later replay of the removed trace.
