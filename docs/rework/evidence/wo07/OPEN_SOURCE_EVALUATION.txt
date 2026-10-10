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

## Final source validation

Validated source: 68a220739bd0249ec0b6ba3df765baf7f0802489.
Validated source tree: e361aa65796301fead03635078e13d6987fe9335.
Branch: codex/pccare-performance-diagnostics-foundation.
Local Release tests: 805 passed, 0 failed, 111 skipped, 916 total.
New performance tests: 36 passed, 0 failed, 0 skipped. These are included in the application total.
Preserved updater tests: 128 passed. Separate SecurityLab tests: 36 passed.
The baseline had 769 passed and 111 skipped. The increase is 36 performance cases.
See GITHUB_RESULT.txt for the final evidence commit and Windows CI status.
Phase 1 remains NO-GO. Real-world causal accuracy has not been independently measured.

## Candidate-specific qualification

| Dimension | TraceEvent 3.2.8 | CsWin32 0.3.358 | LibreHardwareMonitor 0.9.6 |
|---|---|---|---|
| Stable release and maintenance | Official v3.2.8; active upstream | Official v0.3.358; active upstream | Official v0.9.6; active upstream |
| License | MIT | MIT | MPL-2.0; preserve applicable source notices |
| Security | Zero listed public advisories; no absence guarantee | Zero listed public advisories; build-only exposure | Zero listed public advisories; driver boundary needs review |
| .NET 10 | Package load and builds executed | Generated bindings compiled | Runtime integration NOT TESTED |
| Windows 10/11 | Windows 11 executed; Windows 10 NOT TESTED | Reviewed APIs support Windows; app minimum is Windows 11 | Sensor and driver qualification NOT TESTED |
| Privilege | Kernel capture uses existing admin token | Selected APIs work with nonadmin token | Some sensor access needs drivers or elevation |
| CPU and memory cost | Measure with runner capture | Included in measured fallback probe cost | NOT TESTED |
| Long-duration stability | 30-second captures; long stress NOT TESTED | Counters disposed; long stress NOT TESTED | NOT TESTED |
| Errors and cancellation | Typed errors; owned session disposal | Nullable failures; collector cancellation | NOT TESTED |
| Privacy | Numeric events only; salted identifiers | No paths or process command lines | Sensor identifiers require review |
| Integration cost | New collector and bounded numeric queue | Eight reviewed generated user-mode bindings | Deferred driver and sensor policy |

LibreHardwareMonitor 0.9.6 includes PawnIO 2.2 and installer changes.
This work does not automatically install PawnIO or another hardware driver.
PDH supplies existing Windows counters. WPR and PerfView remain independent comparison tools.
The application uses no raw ETL persistence or external upload.

## Windows CI execution

CI run [38081909285](https://github.com/scarlel96-design/PC-Care/actions/runs/38081909285) completed successfully for source 68a220739bd0249ec0b6ba3df765baf7f0802489.
The hosted Windows runner reported elevated=true. It received 141992 numeric ETW records.
Its combined event-loss count was 27304. Zero newly owned ETW sessions remained after collection.
The snapshot reported no ETW startup, processing, loss-query, or cleanup error.
The capture lasted 30.064777 seconds with 29 polling samples.
The probe used 1.000444% whole-machine CPU and 59.468750 MiB sampled peak working set.
The nonadmin local host remains unable to execute kernel ETW. Its fallback measurement was verified separately.
The hosted runner smoke is not a disposable-VM UAC, reboot, GUI, or independent PerfView validation.
Event delivery and cleanup do not establish accurate causal attribution.
Event loss occurred. ETW event totals and per-process contributions are incomplete.
