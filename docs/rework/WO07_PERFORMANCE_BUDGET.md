# WO-07 performance budget

Status: PARTIAL. Three 30-second host captures measured actual costs.
See PERFORMANCE_BUDGET_RESULT.txt for the final measurements.

## Collection limits

| Limit | Value |
|---|---|
| Default duration | 30 seconds |
| Maximum duration | 120 seconds |
| Poll interval | 1–5 seconds |
| CPU saturation interval | At least 2 seconds |
| Maximum retained samples | 120 |
| Process retention | 128 per sample |
| Logical CPU requests | 128 |
| ETW queue | 32–4096 numeric events |
| ETW session buffer setting | 8 MB |
| ETW cleanup wait | 5 seconds |
| Disk/volume groups | 64 |
| Concurrent collector per process | 1 |

Queue bounds do not prove a fixed total ETW memory allocation.
Process.GetProcesses temporarily allocates all enumerated process objects. Each object is disposed.
Native counter initialization can consume part of the collection deadline.
Final elapsed time includes initialization and cleanup. It can exceed the sampling deadline.

## Measurement method

Measure collector process CPU time before and after collection.
Calculate machine CPU cost as CPU seconds / wall seconds / logical processors * 100.
Measure peak sampled process working set in the standalone probe.
Measure analysis elapsed time with Stopwatch.
Working set includes .NET and the probe. It is not the incremental WinUI application memory cost.
No isolated idle state was enforced. The host performed other development and test work.
Differences between runs do not establish a performance improvement.

## Provisional acceptance targets

Target less than 1% whole-machine CPU cost and less than 128 MiB probe working set for nonadmin fallback.
Require UI updates no more frequently than the configured poll interval.
Keep elevated ETW, low-spec hardware, maximum-duration stress, and real GUI responsiveness qualification open.
The provisional budgets do not certify all hardware or workloads.

## Validation boundary

Phase 1 release remains NO-GO. This branch does not authorize production deployment.
Windows 10, disposable VM, GUI interaction, and independent PerfView comparisons remain NOT TESTED.
Elevated kernel ETW execution remains BLOCKED on the available nonadmin host.
No production trust gate, updater, vault writer, deletion engine, or user settings changed.
