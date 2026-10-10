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
