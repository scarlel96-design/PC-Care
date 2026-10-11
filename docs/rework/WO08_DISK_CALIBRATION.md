# WO-08 disk calibration

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

CsWin32 generates PdhGetFormattedCounterArrayW for wildcard PhysicalDisk counters.
The parser bounds its native array allocation to 1 MiB.
It excludes _Total and exports at most 64 physical counter instances.
Each instance keeps throughput, IOPS, latency, queue, and active-time observations.
Session-salted identifiers hide counter instance names.
These identifiers are not stable hardware identities across sessions.
HDD, SATA SSD, and NVMe classification remains Unknown.
The same-session calibration contract requires matching identity, matching workload, and eight valid measurements.
It reports observed median changes. It does not infer a cause or recommended repair.
Per-disk candidate thresholds remain conservative generic observations.
Device-specific learned baselines and verified disk-number mapping remain open.
Bounded synthetic I/O executed. Cached I/O and short bursts limit interpretation.
See DISK_TEST_RESULT.txt for actual execution and reference gaps.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.

## Observed disk instances

The local physical host exported three PhysicalDisk instances.
The CI runner exported two PhysicalDisk instances.
The eight bounded local I/O and CPU/memory workloads completed.
CI completed those eight workloads and one overlapping WPR CPU workload.
These captures measure counter instances. They do not validate stable hardware mapping or device-type classification.
Independent disk metric error remains NOT TESTED.
