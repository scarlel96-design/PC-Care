# WO-08 ui validation

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

The existing WinUI diagnostics screen displays independent resource measurements.
Korean labels distinguish missing measurement, insufficient evidence, candidates, and expected workload.
The screen displays native ETW loss, application queue drops, malformed input, and pending input separately.
Permission failure leaves allowed counter measurement visible.
Range reduction explains that ETW stopped while independent counters continued.
The screen separates evidence coverage and measurement quality from unverified accuracy.
The generation guard prevents unloaded-page progress and final-state updates.
The adapter retains CanAutoApply=false and no repair target.
Presentation contracts and XAML compilation passed.
Live start, stop, navigation, repeat, DPI, keyboard, screenreader, and motion tests remain NOT TESTED.
Raw technical evidence still needs a dedicated Korean explanation layer.
A ranked process I/O table and elapsed UI cancellation benchmark remain open.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.
