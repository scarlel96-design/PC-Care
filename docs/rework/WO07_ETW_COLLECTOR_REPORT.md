# WO-07 ETW collector report

Status: PARTIAL. Parser and controller implemented. Elevated collection remains BLOCKED locally.

## Implemented behavior

Create a unique PCCare.Diagnostics session with NoRestartOnCreate.
Enable DiskIO, MemoryHardFaults, Profile, Process, Thread, and ContextSwitch kernel keywords.
Retain numeric event fields only. Do not subscribe to file-name payloads or command lines.
Use an 8 MB TraceEvent session buffer setting and a 4096-record application queue.
Count native lost events and application drops separately before returning their sum.
Bound each drain to queue capacity. Continuous producers cannot extend one drain indefinitely.
Close the owned session after cancellation, completion, or failure.
Wait at most five seconds for the processing task during cleanup.
Return an error if cleanup fails.

## Privilege and collision boundaries

The host reports TraceEventSession.IsElevated=false.
The collector reports ETW:PermissionDenied and retains CPU, memory, and PDH results.
It does not request UAC or install a service or driver.
A process-wide gate rejects concurrent local collectors.
Unique names prevent attachment to another collector's ETW session.
Cross-process hostile session exhaustion remains NOT TESTED.

## Evidence and limitations

EventBuffer tests exercise overflow and malformed records using synthetic events.
Actual Windows captures verify the nonadmin fallback and nullable hard-fault/context-switch fields.
Synthetic events do not prove kernel provider delivery, accurate PID attribution, or ETW cleanup under reboot.
The CPU sample and disk event supplements are not used as confirmed causal findings.
Event loss prevents complete event attribution. Independent PDH observations retain their own coverage.
ETL replay, reference ETL comparison, and elevated long-duration stress remain deferred.
Do not describe queue tests as actual ETW E2E success.

## Validation boundary

Phase 1 release remains NO-GO. This branch does not authorize production deployment.
Windows 10, disposable VM, GUI interaction, and independent PerfView comparisons remain NOT TESTED.
Elevated kernel ETW execution remains BLOCKED on the available nonadmin host.
No production trust gate, updater, vault writer, deletion engine, or user settings changed.

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

The first CI run, 38081271482, completed its automated checks but collected no kernel events.
Opening TraceEventSession.Source before EnableKernelProvider started an ordinary session.
TraceEvent then rejected kernel-provider activation because the session was already active.
The final source enables the kernel provider before opening Source.
Errors now identify the failing stage and numeric HRESULT. They do not expose exception paths.
The CI smoke gate requires actual event delivery and zero remaining owned sessions on an elevated runner.
Reference: https://raw.githubusercontent.com/microsoft/perfview/v3.2.8/src/TraceEvent/TraceEventSession.cs
The first CI result is not evidence of successful elevated collection.

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
