# WO-08 product readiness

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

WO-08 is not complete against every requested technical acceptance criterion.
Phase 1 release remains NO-GO.

## Remaining priorities
1. Complete aligned independent WPR/WPA interpretation for CPU, memory, disk, and mixed workloads.
2. Measure false positives, false negatives, Unknown, and contribution error on a labeled dataset.
3. Validate real PID reuse and missing process lifecycle events.
4. Capture terminal native loss and actual ETW buffer allocation without optimistic inference.
5. Map counters to verified stable physical devices and device-specific repeated baselines.
6. Run live GUI accessibility, navigation cancellation, and event-burst tests.
7. Measure representative 8-GiB and low-core hardware with HDD and SSD variants.
8. Complete the existing production signing and installed-update P0 gates.

## Privacy and retention
Production collection remains local and in memory by default.
The page does not upload reports or save raw ETL.
The explicit test probe saves numerical snapshots with anonymous session identifiers.
The workload harness uses synthetic files and excludes real vault data.
Raw ETL capture runs only in the isolated reference harness.
The harness records the trace hash, then removes the generated raw trace.
Operator policy: remove temporary workload files within seven days after review.
This retention policy is manual. No automatic seven-day retention service is claimed.
Published synthetic evidence remains versioned for audit.

## Next work
Continue independent validation before enabling performance recovery actions.
Develop vault, secure deletion, and broad UI changes in separate work orders.
Investigate maintained open-source options before new platform collection code.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.

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

## Acceptance criteria audit

| WO-08 criterion | Status | Evidence or remaining work |
|---|---|---|
| Separate loss causes | PASS | Native, queue, malformed, processed, and pending counters are separate. Terminal interval remains unknown. |
| Event-volume profiles | PASS | Three bounded profiles ran on the CI runner. Detailed mode reduced scope after loss. |
| CPU, memory, disk quality | PARTIAL | Independent counters continue. Short windows and generic quality penalties need further calibration. |
| Lifetime identifiers | PASS | Shared registry and synthetic lifetime/PID reuse tests passed. |
| ETW/API correlation | PARTIAL | Eleven General identifiers matched. Independent attribution error remains unmeasured. |
| Top resource selection | PARTIAL | CPU and memory priorities passed. Low-CPU, high-I/O polling selection remains incomplete. |
| Device-specific disk analysis | PARTIAL | Per-instance PDH works. Stable device identity and hardware classes remain Unknown. |
| Controlled real workloads | PARTIAL | Eight bounded scenarios ran. Full memory pressure and all requested process scenarios lack real validation. |
| Independent reference comparison | PARTIAL | WPR capture ran with 39,336 dropped events. Interpretation and ground truth remain open. |
| Quality versus accuracy | PASS | Separate fields exist. Independent accuracy remains null. |
| Live GUI usability | NOT TESTED | Compilation and presentation tests passed. Interactive and accessibility checks remain open. |
| Low-end physical hardware | NOT TESTED | Current host and CI measurements do not represent all requested devices. |
| Security/update regression | PASS | Updater 128 and SecurityLab 36 passed. Existing engine source remained unchanged. |

The controlled workload set has no independent labels for actual root causes.
No actual precision, recall, process contribution error, or disk metric error is asserted.
The General profile avoids high-volume profiling and context switches at the cost of CPU event attribution.
Independent PDH and Process API CPU observations remain available.
Detailed attribution requires further loss and lifecycle validation before product completion.
