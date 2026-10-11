# WO-10 execution report

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Implemented outcome

CPU reference analysis now retains legacy estimates beside SDK scheduling observations.
Scheduling bins preserve Idle, missing, unknown, and overlapping time.
Unqualified reference semantics cannot generate accepted accuracy statistics.
PDH CPU is measured separately. Empty PDH samples produce Unknown.
ReferenceComparison now counts production InsufficientEvidence categories correctly.
The isolated recovery tool validates consent, identity, journal, movement, and original-byte restoration.
The product retains plans and Dry Run only.
A separate UI Automation harness and accessible control identifiers were added.

## Verified local results

| Group | PASS | SKIP | FAIL |
|---|---:|---:|---:|
| WO-09 baseline application | 915 | 111 | 0 |
| WO-10 full application | 957 | 111 | 0 |
| New WO-10 cases, included above | 42 | 0 | 0 |
| All performance cases, included above | 188 | 0 | 0 |
| Updater cases, included above | 128 | 0 | 0 |
| SecurityLab | 36 | 0 | 0 |

Six forced-termination recoveries restored exact fixture bytes.
Hardlink and junction substitutions were rejected.
Actual interactive GUI remains NOT TESTED.
Independent CPU accuracy remains PARTIAL until semantics and WPA agreement are qualified.
Read GITHUB_RESULT.txt for the remote HEAD, tree, CI source, and publication history.

## Facts unavailable from a source-only review

The local host used Windows build 26200, SDK 10.0.401, and 24 logical processors.
The measurement process used a normal token.
ETW permission was denied locally. PDH CPU returned no valid samples.
CPU and memory observations used the permitted fallback.
Concurrent regression tests affected the thirty-second observation.
Initial restricted testhost IPC attempts failed. Authorized reruns returned the recorded passing counts.
A raw SDK property experiment failed to compile and was restored before publication.
The final executable source tree excludes that experiment.
No user vault, Downloads files, system settings, installed product, or shutdown command was used.
Synthetic execution keys remain in ignored local fixtures and are excluded from repository evidence.
Source-copy rollback and actual fixture rollback are separately documented.

## Changes and reasons

Read CHANGED_FILES.txt for the exact implementation inventory.
Read VERIFICATION.txt for baseline, modified, and rollback commands and hashes.
Read each required report for remaining risks and follow-up conditions.

## Final CI result

Windows CI [38113296563](https://github.com/scarlel96-design/PC-Care/actions/runs/38113296563): SUCCESS.
Verified source: c23802b2adc7be63c8c15ab6cd313ec9be0a05db.
Release build: zero warnings and errors.
Application: 957 PASS, 111 SKIP, zero FAIL.
Performance: 188 PASS. Updater: 128 PASS. Both groups are included in the application total.
SecurityLab: 36 PASS. SDK timestamp contracts: 10 PASS.
Six separate-process crash recoveries restored the exact synthetic fixture bytes.
CI rejected hardlinks, junctions, and symbolic links.
The full TRX result nodes contain 111 NotExecuted entries.
Its summary counter reports notExecuted=0. Counts use the actual result nodes.
Twelve bounded CPU reference trials executed. Qualified truth remains 0/12.
The historical all-six-near-100-percent claim conflicts with the retained JSON.
Actual interactive GUI and low-end physical hardware remain NOT TESTED.
Report publication commits contain documentation and evidence only.
Their source and workflow blobs must match the verified source commit.


## Baseline narrative correction

WO-09 trials 1-3 used sustained CPU workloads and returned near-100-percent sampled medians.
Peak trials 4-6 returned medians of 1.72558, 28.35959375, and 1.8503375 percent.
The retained JSON contradicts the work-order premise that all six estimates were near 100 percent.
The original evidence remains unchanged.
WO-10 does not claim a proven universal 100-percent calculation defect or its repair.
