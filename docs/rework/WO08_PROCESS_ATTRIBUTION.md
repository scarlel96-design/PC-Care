# WO-08 process attribution

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

Polling inspects low-cost CPU time and working set metadata before detail selection.
It observes up to 8192 processes and disposes every enumerated process wrapper.
The selection combines CPU leaders and memory leaders within the 128-entry detail budget.
It no longer selects the first 128 enumeration entries.
Access failures, accessible entries, selected entries, and metadata omissions are recorded.
CPU percentage divides CPU-time delta by elapsed seconds and all logical processors.
The first sample has no CPU delta. It remains Unknown.
ETW contribution exports CPU sample counts, read/write bytes, operations, and observed event bounds.
CPU sample counts are not converted into verified CPU percentages.
ETW contributions contain observed accepted events only. They do not represent all system activity.
Session totals retain at most 4096 identities. Interval aggregation retains at most 128 identities.
Unknown or overflowed attribution increments UnattributedEvents.
Low-CPU, high-I/O processes can be absent from polling selection.
ETW totals retain observed I/O contribution when lifetime resolution succeeds.
ETW process I/O totals are available in snapshots. A dedicated ranked UI table remains open.
Real process contribution error and sample coverage accuracy remain NOT TESTED.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.

## Final Windows CI evidence

Run 38084989987 completed successfully on source commit `77e19b01f1baa579811bbd122b062abaaa460ba1`.
The runner used Windows build 26100, four logical processors, and an elevated token.
The full app passed 848 tests, failed zero tests, and skipped 111 tests.
The performance subset passed 79 tests. The updater subset passed 128 tests.
SecurityLab passed 36 tests in its separate project.
The Release build reported zero warnings and zero errors.
The measurement harness completed with exit code zero and `WO08_MEASUREMENT_COMPLETED`.
All 13 saved collector captures had verified cleanup and zero remaining owned sessions.
Native loss counters were queried before stop. The terminal interval remains unknown.
General mode omits CPU profile and context-switch event streams.
LowOverhead mode tracks lifetimes without resource events. Its zero resource count is expected.
Detailed mode dropped 2,106 queue events and disabled ETW after loss.
Detailed mode recorded 10,323 unattributed events. Attribution completeness is not established.
Independent PDH and system snapshots continued after ETW scope reduction.
Short CPU and mixed captures had fewer than eight samples. They cannot prove persistent saturation.
These different profiles and workloads do not establish a WO-07 improvement percentage.
The measurements do not establish physical low-end hardware performance.

| Profile | Received | Native loss before stop | Queue drop | Malformed | Accepted / processed / pending | CPU % machine | Peak MiB | Range reduced |
|---|---:|---:|---:|---:|---|---:|---:|---|
| Detailed | 16007 | 0 | 2106 | 0 | 13901 / 13901 / 0 | 1.275 | 61.785 | True |
| General | 2692 | 0 | 0 | 0 | 2692 / 2692 / 0 | 1.607 | 54.035 | False |
| LowOverhead | 0 | 0 | 0 | 0 | 0 / 0 / 0 | 1.142 | 52.680 | False |

## Observed process correlation

The General CI capture exported 11 ETW process identifiers.
All 11 also appeared in Process API snapshots from the same collection.
This proves identifier correlation for observed records. It does not prove attribution accuracy.
Detailed mode correlated nine of ten ETW identifiers with polling snapshots.
Short-lived processes can finish between polling intervals. Missing matches require further review.
The Detailed capture recorded 10,323 unattributed events after resource-event loss.
Real PID reuse and contribution errors still require an independent labeled reference.
