# WO-08 open source evaluation

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

TraceEvent 3.2.8: MIT; retained. Official release tag and NuGet metadata reviewed.
CsWin32 0.3.358: MIT; retained. Official release tag and NuGet metadata reviewed.
No dependency version upgrade occurred. Release .NET 10 x64 compilation and Windows execution passed.
NuGet audit reported no known vulnerable packages with the configured feeds, including transitive packages.
This audit is a feed result, not a complete security certification.
TraceEvent remains the ETW parser and session implementation. CsWin32 generates PDH bindings.
WPR/WPA remains the independent reference technology. Independent interpretation remains open.
LibreHardwareMonitor remains optional. Its core license is MPL-2.0. No sensor driver was installed.
Temperature and clock remain Unknown.
Sources:
https://github.com/microsoft/perfview/releases/tag/v3.2.8
https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent/3.2.8
https://github.com/microsoft/CsWin32/releases/tag/v0.3.358
https://www.nuget.org/packages/Microsoft.Windows.CsWin32/0.3.358
https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
https://learn.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-recorder
https://learn.microsoft.com/en-us/windows/win32/api/evntrace/ns-evntrace-event_trace_properties
https://learn.microsoft.com/en-us/windows/win32/etw/configuring-and-starting-an-event-tracing-session

The integration reuses existing .NET 10 contracts, counter collection, and TraceEvent parsers.
PDH and native snapshots support ordinary user processes. Kernel ETW needs administrator rights.
No new administrator service exists. Native errors remain explicit.
Session cancellation, queue limits, collection deadlines, and cleanup remain bounded.
Long-duration and low-end hardware validation remain open.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.
