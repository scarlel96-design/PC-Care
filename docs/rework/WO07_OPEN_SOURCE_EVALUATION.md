# WO-07 open-source evaluation

Status: PARTIAL. Evaluation date: 2026-10-11.

| Candidate | Stable version | License | Decision |
|---|---|---|---|
| [TraceEvent](https://github.com/microsoft/perfview) | 3.2.8 | MIT | Adopt bounded real-time ETW parsing. |
| [CsWin32](https://github.com/microsoft/CsWin32) | 0.3.358, 2026-10-08 | MIT | Adopt generated PDH and system snapshot bindings. |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.6, 2026-02-14 | MPL-2.0 | Defer sensors and driver access. Report Unknown. |
| Windows ETW APIs | Windows inbox | Windows SDK terms | Use through TraceEvent; avoid a second parser. |
| PDH | Windows inbox | Windows SDK terms | Use generated English counter bindings. |
| WPR / WPA | Windows performance tools | Microsoft tool terms | Reserve for independent reference captures. |

## Evaluation dimensions

TraceEvent targets netstandard2.0. Its package loaded under .NET 10 on Windows 11.
CsWin32 generated bindings compiled under .NET 10 with zero warnings.
The native system snapshot measured CPU, physical memory, and commit usage.
The sandbox POC could not read PDH. The unrestricted nonadmin process read PDH successfully.
Kernel ETW requires elevated access. This application does not request elevation.
Windows 10 compatibility is based on API support. Windows 10 execution remains NOT TESTED.
LibreHardwareMonitor integration and runtime compatibility remain NOT TESTED.

TraceEvent and CsWin32 have recent official releases and active upstream development.
Public GitHub security advisory lists returned zero entries for all three repositories.
Zero public entries do not establish absence of vulnerabilities.
TraceEvent 3.2.8 includes parser, path, source download, and memory-growth hardening.
PC-Care does not use symbol downloads, source downloads, or automatic ETL persistence.
The NuGet transitive audit found no listed vulnerable packages on 2026-10-11.
CsWin32 is a build-only generator. Its metadata dependencies include preview and experimental packages.
Only reviewed user-mode functions are requested. No driver APIs are generated.

## Resource, cancellation, and privacy evaluation

TraceEvent has session disposal and event-loss counters. Application budgets remain necessary.
Bound ETW buffers to 8 MB and the application queue to 4096 records.
Bound measurement duration to 120 seconds. Bound process retention to 128 entries per sample.
Reduce polling frequency during CPU saturation. Dispose all process and counter handles.
Record actual collector CPU time and peak working set during repeated measurements.
Long-duration and low-spec hardware validation remain NOT TESTED.
Raw ETW events can contain sensitive paths. Retain numeric records only.
Hash process identities with a per-session salt. Do not retain paths or process command lines.

## Selection gate

POC command: dotnet run --project tools/SmartPerformanceDoctor.DiagnosticsProbe -c Release -p:Platform=x64 --no-restore
Input: local read-only system counters. No user files.
Result: TraceEvent 3.2.8.0 loaded; elevated=false; native CPU 13.409742120343838%.
Physical memory was 34028429312 bytes. Available memory was 10789687296 bytes.
Commit was 55381417984 bytes; limit was 64093200384 bytes. Exit code: 0.
The initial POC returned no PDH counters. Later repeated Windows captures measured disk and logical CPU counters.
The selected combination uses Windows collection APIs and preserves PC-Care-specific analysis.

## Existing code review

CoreDashboardBridgeService reports app memory, free space, and report inventory.
CareSystemProbes performs point-in-time status checks and parses several string responses.
RootCauseScoringService ranks rule metadata and token overlap. It does not calculate statistical confidence.
SystemCareService mixes status observations with repair tasks. Its current behavior remains preserved.
The new performance layer does not reuse cleanup scanners or repair execution.
The adapter reuses CareFinding and CareFindingQualityGate. It always disables automatic application.

## Primary references

- https://github.com/microsoft/perfview/releases/tag/v3.2.8
- https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent/3.2.8
- https://github.com/microsoft/CsWin32/releases/tag/v0.3.358
- https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6
- https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhaddenglishcounterw
- https://learn.microsoft.com/en-us/windows/win32/etw/configuring-and-starting-an-event-tracing-session

The existing application declares Windows 11 minimum build 22000. Windows 10 product support is not established by this work.
