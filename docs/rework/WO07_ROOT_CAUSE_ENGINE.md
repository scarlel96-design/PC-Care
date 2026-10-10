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
