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

Final-source capture lasted 30.0466024 seconds with 25 samples.
Its machine CPU cost was 0.511358%.
Its sampled peak probe working set was 56.101562 MiB.
Analysis took 25.9188 ms.
The three earlier repeated captures and the final-source capture are preserved separately.

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

## Maximum-window execution

The 120-second nonadmin capture completed in 120.0318015 seconds with 117 samples.
Its machine CPU cost was 0.088952%. Its sampled peak working set was 60.371094 MiB.
The elapsed boundary includes initialization and cleanup. No unbounded session remained active in this fallback capture.
The elevated CI probe cost was 1.000444% of the four-processor runner.
The below-1% provisional target applies to the nonadmin fallback path.
Elevated ETW queue-loss reduction and workload-specific budget qualification remain open.
