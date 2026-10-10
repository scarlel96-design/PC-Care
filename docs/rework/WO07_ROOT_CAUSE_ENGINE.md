# WO-07 evidence rules

Status: PARTIAL. Deterministic pressure rules passed synthetic tests.
Actual causal accuracy remains NOT TESTED.

## Rule definitions

| Candidate | Necessary evidence |
|---|---|
| CPU saturation | CPU >=90%; >=80% matching readings; continuous >=8 seconds |
| Memory pressure | Max(physical usage, commit ratio) >=90%; same persistence requirements |
| Disk I/O pressure | Queue >=1; operations >0; latency >=20 ms; same persistence requirements |
| Process contribution | CPU candidate; stable identifier coverage >=80%; median CPU >=20% of machine |
| Transient work | Some high readings without sustained-pressure criteria |
| Insufficient evidence | Fewer than 8 valid readings, coverage <80%, duration <10 seconds, or invalid timestamps |

High throughput alone does not establish disk pressure.
The disk threshold is a screening threshold, not a hardware-specific performance grade.
NVMe, HDD, and mixed-device calibration remain NOT TESTED.
Memory page reads are not an exact count of all hard faults.
The combined disk total can hide a single-device bottleneck.

## Confidence contract

Confidence = valid coverage * min(valid samples / 20, 1) * min(observed span / 20 seconds, 1).
Invalid time or insufficient evidence produces zero confidence.
Confidence measures observation sufficiency within one capture. It is not diagnostic accuracy or causation probability.
A value of 1 does not prove independent reproduction.
ETW loss appears in limitations. These three rules use independent native or PDH inputs.
Cross-run reproducibility is not included until workload matching has been validated.

## Normal workload handling

The user can label an intentionally intensive workload.
Sustained pressure then receives ExpectedWorkload severity.
Unspecified workload produces a candidate, without an abnormal-software claim.
Automatic identification of Windows Update, games, rendering, or antivirus activity remains unimplemented.
No finding provides an optimization command or automatic application.
The next check asks for repeat measurement and workload identification.

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

CPU findings use CsWin32.GetSystemTimes. Memory findings use GlobalMemoryStatusEx and GetPerformanceInfo.
Disk findings use CsWin32.PDH. Process contributors use System.Diagnostics.Process.
MeasurementSummary uses raw measured disk latency values, including low latency readings.
It does not substitute zero values for readings that fail a pressure predicate.
Missing memory byte limits remain Unknown. Synthetic tests protect these contracts.

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
