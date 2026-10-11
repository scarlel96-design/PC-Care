# WO-09 execution report

Status: PARTIAL.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
CI source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Implemented results

The independent ReferenceProbe reads WPR ETL through Microsoft TraceProcessing 1.12.10.
It exports anonymous CPU, process lifetime, and disk I/O measurements.
ReferenceQualification compares only common observation windows.
Loss, gaps, invalid windows, and insufficient bins prevent qualified accuracy claims.
ReferenceComparison now excludes InsufficientEvidence from true-positive counts.

The process selector includes resolved ETW I/O leaders alongside CPU and memory leaders.
PhysicalDiskMapping binds unambiguous disk numbers to private-key HMAC identifiers.
The read-only storage tool repeated three device mappings twice.
One device was NVMe SSD. Two device types remained Unknown.
Application-wide persistent disk identity remains incomplete.

RecoveryPlanner generates compiled, read-only review plans from existing Finding contracts.
Two action types are supported by current resource evidence.
Six additional action labels remain blocked until suitable evidence contracts exist.
Dry Run exposes preconditions, privileges, risk, consent, rollback limitations, and verification metrics.
It has no system executor, external rule loader, or write path.
The diagnostic page displays actual Dry Run results.
DiagnosticLifecycle prevents overlapping collection and stale results after navigation.

## Local verification

| Test group | PASS | SKIP | FAIL | Evidence |
|---|---:|---:|---:|---|
| WO-08 baseline | 848 | 111 | 0 | LOCAL_BASELINE.trx |
| Modified application | 915 | 111 | 0 | LOCAL_FULL.trx |
| New WO-09 cases, included above | 67 | 0 | 0 | LOCAL_WO09.trx |
| SecurityLab | 36 | 0 | 0 | LOCAL_SECURITYLAB.trx |

The solution Release build returned zero warnings and zero errors.
Local regression assemblies preceded the final GUI binding and reference-parser diagnostic changes.
Windows CI validates the exact final executable source.
The final publication commit adds reports and evidence only.
All commands, literal logs, environments, and exit codes appear in the evidence directory.
An initial sandbox testhost attempt timed out. The authorized unrestricted rerun completed successfully.
The first Git Bash rollback attempt failed during PowerShell module loading.
The corrected rollback script uses the .NET SHA-256 API. Its rerun returned exit code zero.

A 30-second normal-token observation collected 28 samples.
Collector CPU was 0.1908352915 percent of the whole machine.
Peak working set was 66.875 MiB. Managed allocation was 39.9478 MiB.
Analysis took 24.2128 milliseconds.
Concurrent builds and tests affected the environment.
This observation does not qualify low-end devices or independent diagnostic accuracy.
ETW access was denied for the normal token. This condition remains explicit.

The four-finding Dry Run produced one preview and three blocked plans.
Its output recorded ExecutionEnabled=False and SystemMutations=0.
Headless lifecycle tests passed. Live GUI validation remains NOT TESTED.

## Changes and rollback

Read GITHUB_RESULT.txt for the source commit, publication history, and changed-file inventory.
Read VERIFICATION.txt for the preserved source hashes and exact fixture commands.
ROLLBACK.sh restores only a designated independent source copy.
It does not restore an installed product or test an installation transaction.
Use a reviewed Git revert to undo repository commits.

## Remaining work

Independent CPU results and exclusions are recorded in DIAGNOSTIC_ACCURACY and ACCURACY_METRICS.json.
Independent memory and disk pressure truth remain NOT TESTED.
Cross-parser process contribution error remains NOT TESTED.
Actual PID reuse remains NOT TESTED.
Live GUI, high DPI, accessibility, and event-storm interactions remain NOT TESTED.
No production signing or installation qualification is established by this work.

## Facts unavailable from a source-only GitHub review

The local host used a normal, non-administrator token and 24 logical processors.
The local OS reported build 10.0.26200. The .NET SDK reported 10.0.401.
The local ETW path reported permission denial. The permitted counter path returned measured data.
The 30-second observation overlapped builds and automated tests.
It cannot establish product overhead on an idle PC or a low-end PC.

The session did not open the installed product, real vault data, or user files for workload testing.
No performance-recovery command ran. Existing updater recovery tests used synthetic installation directories.
No user installation transaction changed during WO-09.
The source-copy restoration exercised the classifier fixture, not an installed application rollback.
No native GUI interaction was observed. XAML compilation and lifecycle unit tests supply separate evidence.

Local full-test results preceded the final GUI and reference-parser changes.
The final hosted CI tests identify the exact executable source commit.
Public TRX copies mask local user and computer identifiers.
TEST_EVIDENCE_REDACTION.json records original and published hashes.
Raw local logs remain in the ignored artifacts directory.

The CI harness runs on one hosted Windows image with an elevated runner token.
It is not a representative hardware fleet or a dedicated Windows 11 installation VM.
Private ETL is deleted after successful SDK processing, even if pressure qualification fails.
The published trace hash supports identification. It cannot substitute for retained ETL reanalysis.

## Final Windows CI observations

CI [38105566632](https://github.com/scarlel96-design/PC-Care/actions/runs/38105566632) completed successfully on source `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
The source tree is `bfa20d37d8adca1d365d96efbd40983aff988678`.
The runner reported Windows NT 10.0.26100, four logical processors, and an elevated token.
All six trials used a 28-second common comparison window.

| Trial | Workload | WPR lost events | WPR lost buffers | SDK CPU gaps | Valid aligned CPU samples | Independent lifetimes | Collector machine CPU % | Collector peak MiB |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | cpu | 0 | 0 | 121988 | 111988 | 6 | 0.675593 | 56.777 |
| 2 | cpu | 0 | 0 | 121816 | 111988 | 6 | 0.740397 | 52.855 |
| 3 | cpu | 0 | 0 | 122144 | 111988 | 7 | 0.701551 | 51.773 |
| 4 | peak | 0 | 0 | 121724 | 111988 | 7 | 0.649949 | 54.207 |
| 5 | peak | 0 | 0 | 121852 | 111988 | 8 | 0.766655 | 57.715 |
| 6 | peak | 0 | 0 | 121764 | 111988 | 9 | 0.818504 | 53.594 |

All six workload commands completed. All six SDK analysis commands returned exit code zero.
Zero trials passed the predeclared CPU reference qualification gate.
The SDK gap counts are estimated unaccounted sampling periods. They are separate from native ETW loss.
Precision and Recall remain null. Unknown is 18 across three resource categories and six trials.
True Positive, False Positive, False Negative, and True Negative are zero qualified classifications.
These zeros do not establish zero product errors.
Memory and disk pressure truth remain Unknown.
Process contribution error and disk measurement error remain unmeasured.

The final parser accepted converted timestamps and durations from partial SDK contexts.
Each trial supplied 111,988 valid CPU samples in the aligned window.
Invalid timestamp and invalid duration counts were zero in all six trials.
The SDK conversion fixture passed ten cases.
Some CPU bins slightly exceeded 100 percent because sampling weights estimate elapsed activity.
The parser preserves these observations. It does not clamp them into qualified truth.
The zero-gap gate already excludes these trials. The range check remains an additional qualification condition.

PC-Care reported zero native ETW loss and zero application queue drops in all six trials.
All six session cleanup results were VerifiedAbsent.
The native application loss reading occurs before stop. Its terminal interval remains Unknown.
The final trace analyzer used 495.642 to 684.972 milliseconds.
Its peak working set ranged from 121.504 to 145.180 MiB.
Analyzer CPU time was not measured.
The collector values describe one hosted runner. They do not qualify a hardware fleet.
The sequential CPU-then-peak trial order leaves cache and background-work confounding.
The next qualification must investigate the SDK sampling gaps and repeat counterbalanced trials.

## Final automated regression

Application: 915 PASS / 111 SKIP / zero FAIL / 1,026 total.
Performance: 146 PASS, including 67 new WO-09 cases.
Updater: 128 PASS. Performance and updater counts are application subsets.
SecurityLab: 36 PASS, separate project.
SDK timestamp conversion: ten PASS, separate executable fixture.
The existing process crash harness matched 121 expected command results.
Those results test synthetic installation recovery, not the user installation or Windows reboot.
Eight bounded workload smokes completed. Two hosted read-only disk observations completed.
The exact executable source is 888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7.
The final publication commit contains reports and evidence. Its Git identity is recorded by the enclosing commit.
