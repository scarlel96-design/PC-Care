# WO-11 — Before and after measurement

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Local measured results

| Metric | Quarantine | Final deletion |
|---|---:|---:|
| Candidate payload bytes | 8,388,608 | 8,388,608 |
| Quarantined bytes after operation | 8,388,608 | 0 |
| Restorable bytes | 8,388,608 | 0 |
| Final-deleted bytes | 0 | 8,388,608 |
| Available bytes before | 85,416,542,208 | 85,408,141,312 |
| Available bytes after | 85,416,546,304 | 85,416,529,920 |
| Observed volume free-space delta | 4,096 | 8,388,608 |
| Attributable payload space gain | 0 | Unknown |
| Operation time, ms | 170.8965 | 121.8321 |

The quarantine rename retained its payload allocation on the same volume.
Its observed 4,096-byte volume change was not reported as cleanup gain.
The final-deletion volume delta includes concurrent activity.
The implementation leaves its attributable gain null.
Speed improvement remains null. No CPU acceleration or workload speed gain was measured.

## Measurement limitations

This was one synthetic run on the local host.
Different runs can observe different global free-space deltas.
Logical payload bytes are not allocated clusters.
Process working set includes the probe runtime.
The scan CPU value is process CPU time during the scan window.
These samples are not low-end hardware qualification or long-run leak tests.
Disk latency and user workload completion-time improvements remain NOT TESTED.

## Future comparison

Use the same volume, workload, observation window, and cache conditions.
Record before and after measurements independently.
Repeat the workload before claiming a performance improvement.
Report unchanged or negative results without converting cleaned bytes into an improvement percentage.
