# WO-07 test report

Status: PARTIAL. Final counters and commands appear in FULL_TEST_RESULT.txt.

## Predefined synthetic scenario matrix

| Scenario | Expected result | Evidence |
|---|---|---|
| Sustained CPU | CpuSaturation Candidate | 30 readings at 95% |
| Single CPU peak | No sustained candidate | One 99% reading |
| Sequential read | Throughput alone does not qualify | Numeric throughput-only fixture |
| Random I/O | Latency plus queue and operations qualify | 80 ms, queue 4, 10 operations/s fixture |
| Memory pressure | MemoryPressure candidate | Available 5%, commit 95% |
| Mixed processes | Stable CPU contributors | Hashed fixture identifiers |
| Normal background work | ExpectedWorkload or transient | Explicit workload context and peak fixture |
| Event loss | Loss limitation, incomplete attribution | Queue overflow and snapshot loss |
| Permission shortage | Typed denial; fallback retained | Actual nonadmin captures |
| Session conflict | Concurrent collection rejected | In-process gate test |
| Cancellation | Partial evidence and Cancelled | CancellationToken test |
| Collector exception | Evidence retained; typed exception | Synchronous observer throws IOException |
| Invalid values | Unknown | NaN, infinity, negative, and out-of-range fixtures |
| Time mismatch | Zero confidence | Reversed timestamps |
| Too little data | InsufficientEvidence | Empty, sparse, and short windows |

Synthetic classification expects exact categories and no automatic repairs.
Percentile interpolation tolerates 1e-5 numeric error.
A known high-load fixture is a resource-pressure label, not malicious behavior.
Real sequential/random workload generation and independent tool reference traces remain NOT TESTED.

## Test correction

The first full regression failed CollectorExceptionPreservesEvidence.
A two-second deadline expired during counter initialization under concurrent host load.
The intended observer exception was never reached. The collector returned unavailable inputs correctly.
The test now permits 30 seconds and still stops at its first observer exception.
The initial failed log and TRX remain preserved.
The solution build produced AnyCPU output. Direct test project builds produced the required x64 output.
Early no-build invocations read stale x64 binaries. Final evidence uses direct x64 builds.
No existing security test was removed or newly skipped.

## Validation tiers

Automated scenarios, actual nonadmin Windows capture, elevated ETW, GUI, and independent accuracy remain separate.
Passing synthetic categories is not a measured real-world accuracy percentage.
The confidence score is not a percentage of product completion.

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

Additional tests verify memory-pressure duration, raw latency statistics, unknown byte limits, and actual provider labels.
All 15 requested scenario types have a deterministic fixture or a collector-boundary test.
Sequential and random I/O fixtures are numeric replay. They are not generated physical-device workload tests.
Session-conflict testing covers the application gate. Hostile cross-process ETW exhaustion remains NOT TESTED.
Actual nonadmin capture verifies permission denial and retained fallback measurements.
Independent PerfView comparison, GUI interaction, and low-spec measurements remain NOT TESTED.

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
