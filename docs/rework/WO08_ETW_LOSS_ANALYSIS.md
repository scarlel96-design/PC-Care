# WO-08 etw loss analysis

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

The old queue consumer drained approximately once per polling sample.
High-volume context-switch events competed with disk and CPU evidence.
The new consumer drains every 20 milliseconds on a background task.
Native loss, queue drops, malformed input, accepted input, processed input, and pending input are separate.
Queue conservation is checked at terminal stop. Live reads can cross concurrent counter updates.
Native loss remains session-level. No provider-specific loss estimate is invented.
Process lifecycle callbacks are outside ReceivedByKind and queue conservation.
Malformed events never enter aggregation.
The last native query occurs before stop. The final native interval remains unverified.
Any measured native loss or queue drop stops optional ETW collection.
Independent PDH and native sampling continue.
This policy minimizes further loss. It does not reconstruct lost events.
See ETW_LOSS_RESULT.txt and CI_MEASUREMENT_SUMMARY.json.

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
