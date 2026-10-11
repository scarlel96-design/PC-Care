# WO-08 independent accuracy

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

The bounded workload executable runs only through an explicit CLI command.
CPU threads are limited to four. Each workload is limited to 15 seconds.
Memory allocation is limited to 128 MiB. The harness uses 64 MiB.
Synthetic disk files use CreateNew. The harness limits measured I/O to 128 MiB.
Memory allocation on this 32-GiB machine does not prove memory pressure.
Four CPU threads on 24 logical processors do not prove whole-system saturation.
The short CPU peak runs for one second.
Workload timestamps and process CPU time are recorded.
Workload startup and measurement windows are not exactly aligned.
The optional WPR capture requires an isolated GitHub runner.
Existing WPR recordings are preserved. Only the harness-owned recording is stopped.
Raw ETL is hashed and removed from the runner. It is not uploaded.
Successful reference capture alone remains PARTIAL.
No independently interpreted, aligned ground-truth dataset exists yet.
Real precision, recall, false positives, false negatives, and contribution errors remain null.
ReferenceComparison contract tests do not establish actual diagnostic accuracy.
Use a disposable VM with WPT to complete independent CPU, memory, and disk interpretation.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.

## Short-window limitation

The collector increases its interval during CPU saturation.
A 12-second workload can then provide fewer than eight samples.
The analyzer returns InsufficientEvidence for that window.
Use longer aligned reference windows in the next isolated validation phase.
WORKLOAD_OBSERVATION_COUNTS.json records analyzer states without claiming ground-truth accuracy.

## Executed independent reference capture

WPR started and stopped successfully on the isolated CI runner. Both commands returned zero.
The trace contained 300,941,312 bytes before removal.
Its SHA-256 was `0461B00ED7E0628F88166FF92CD1A2200FEC3819ABAF4BE763F8E61AB8F06A31`.
WPR reported: `This trace has dropped 39336 events. Please record this trace again.`
The generated ETL was removed after hashing. No raw ETL was uploaded.
The WPR capture overlaps the recorded bounded CPU workload.
No independent WPA interpretation or aligned category ground truth was completed.
Reference comparison remains PARTIAL. Real diagnostic accuracy remains NOT TESTED.
False positives, false negatives, Unknown counts, precision, recall, and contribution errors remain null for independent accuracy.
WPR loss belongs to its own session. It is not PC-Care queue loss.
Use a dedicated runner and a loss-checked reference configuration for the next comparison.
Save an explicitly authorized trace until independent interpretation completes.
The current hash proves trace creation. It does not permit later replay of the removed trace.
