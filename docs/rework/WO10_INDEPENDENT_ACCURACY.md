# WO-10 independent accuracy

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Predefined experiment

The isolated CI runner repeats twelve bounded, thirty-second workloads.
Order: idle, medium, cpu, peak, mixed, expected-high, expected-high, mixed, peak, cpu, medium, idle.
CPU workers are limited to four and to the runner processor count.
The common comparison window starts two seconds after workload start.
The one-second peak is excluded from this later window. This trial observes post-peak behavior.
Medium duty is not asserted to equal fifty-percent whole-system load.
Four workers cannot guarantee saturation on a high-core host.
WorkloadExecuted does not imply PressureVerified.

## Reference pipeline

WPR records the existing bounded WO-09 file profile.
TraceProcessing reads the retained ETL before trace disposal.
The runner directory uses a protected ACL. Raw ETL files are excluded from upload.
The script records trace SHA256, bytes, stop output, exit codes, and timestamps.
GetSystemTimes and PDH observations are retained alongside sampled and scheduling results.
WPA Exporter comparison is still missing.
All new scheduling references explicitly remain unqualified.
Accuracy denominators exclude unqualified references. Precision and Recall remain null.
No accuracy rate is generalized to user systems.

## Production classifier correction

BottleneckAnalyzer emits InsufficientEvidence-prefixed categories with Severity Unknown.
ReferenceComparison now tests the category contract before the generic Unknown severity.
The fixture uses actual production analyzer output.
The old count was InsufficientEvidence=0 and Unknown=1.
The corrected count is InsufficientEvidence=1 and Unknown=0.
Neither output creates a TrueNegative for missing data.
Read ACCURACY_METRICS.json for observed and excluded trial counts.

## Actual Windows CI observations

Run [38113296563](https://github.com/scarlel96-design/PC-Care/actions/runs/38113296563) completed successfully.
All twelve capture, measurement, stop, and analysis commands returned exit code zero.
The runner used Windows build 26100 and four logical processors.
Each common window lasted 28 seconds.

| Trial | Mode | GetSystemTimes median % | PDH median % | Scheduling median % | Sampled median % | CpuGaps |
|---|---|---:|---:|---:|---:|---:|
| 1 | idle | 1.9531 | 2.0834 | 1.7556 | 1.7507 | 122424 |
| 2 | medium | 4.6154 | 5.2272 | 4.4075 | 4.2136 | 122600 |
| 3 | cpu | 100.0000 | 100.0000 | 100.0000 | 100.0100 | 125353 |
| 4 | peak | 4.6875 | 3.2033 | 3.1230 | 3.0765 | 122785 |
| 5 | mixed | 100.0000 | 100.0000 | 100.0000 | 100.0000 | 125720 |
| 6 | expected-high | 100.0000 | 100.0000 | 100.0000 | 100.0100 | 124492 |
| 7 | expected-high | 100.0000 | 100.0000 | 100.0000 | 100.0100 | 125004 |
| 8 | mixed | 100.0000 | 100.0000 | 100.0000 | 100.0100 | 128944 |
| 9 | peak | 2.6923 | 2.4576 | 2.1891 | 2.1623 | 122764 |
| 10 | cpu | 100.0000 | 100.0000 | 100.0000 | 100.0100 | 124844 |
| 11 | medium | 1.9231 | 1.7732 | 1.7970 | 1.8001 | 122604 |
| 12 | idle | 3.0769 | 2.8665 | 2.4981 | 2.5138 | 122356 |

These medians describe observations. They do not measure independently qualified diagnostic error.
All WPR native lost-event and lost-buffer counts were zero.
Sampling gaps ranged from 122356 to 128944.
Scheduling totals contained zero missing, unknown, and overlapping seconds in these trials.
WPA agreement and DPC/ISR qualification remain absent.
Qualified CPU references: 0/12. Excluded references: 12/12.
CPU trial Unknown count: 12. Three-resource Unknown count: 36.
TP, FP, FN, and TN are zero because no qualified reference entered the matrix.
Precision, Recall, and process contribution error remain null.
These zero counts do not establish zero diagnostic errors.
