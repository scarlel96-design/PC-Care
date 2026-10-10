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
