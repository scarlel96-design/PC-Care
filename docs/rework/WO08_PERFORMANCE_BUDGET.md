# WO-08 performance budget

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

The local host has 24 logical processors and about 32 GiB RAM.
It uses a nonadministrator token even after sandbox escalation.
Each local profile ran for 15 seconds. Local kernel ETW returned PermissionDenied.
Three physical disk counter instances were observed through PDH.
Exact CPU, peak working set, and allocation values appear in LOCAL_MEASUREMENT_SUMMARY.json.
CPU overhead uses collector CPU seconds divided by duration and logical processor count.
Peak working set is sampled. It is not a hardware high-water mark.
ManagedAllocatedBytes is allocation during collection. It is not retained heap size.
Development and normal user activity continued during measurements.
CI VM measurements are separate from local physical-machine measurements.
Low-end dual-core, quad-core, 8-GiB, and HDD hardware remain NOT TESTED.
Actual ETW native buffer occupancy and live UI latency remain NOT TESTED.
No universal low-end performance budget PASS is claimed.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.

## Cancellation measurement

Three nonadmin cancellations returned after 5.5841, 0.2579, and 0.1934 milliseconds.
These measurements exclude live UI and elevated ETW cancellation.
Read CANCELLATION_RESULT.json and the fixture source files for reproduction.

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
