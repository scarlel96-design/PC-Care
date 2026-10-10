# WO-07 diagnostic privacy policy

Status: PASS for inspected data flow. External privacy audit remains NOT TESTED.

## Data retained

Retain numeric CPU, memory, disk, process-resource, and uptime observations.
Use a new random salt for each capture's process identifiers.
Bind process sampling identifiers to PID and start time.
Use separate salted PID identifiers for ETW supplemental attribution.
Retain drive letters and numeric free-space percentages. Do not scan drive contents.
Count Run-key values without storing names or executable paths.
Retain UTC timestamps and OS version for comparable measurements.

## Data excluded

Do not retain process names, command lines, file paths, usernames, or file contents.
Do not inspect vault contents or run secure deletion.
Do not write raw ETL files or download symbols or sources.
Do not transmit measurements outside the local machine.
Return typed error codes instead of exception messages that can contain private paths.

## Retention

The application stores the capture in page memory only.
Starting another capture replaces the displayed findings. Leaving the page cancels collection.
The application does not automatically create diagnostic history files.
The probe writes JSON only when an explicit output path is supplied.
User-selected exports remain until the user removes them.
Repository evidence contains synthetic test data and masked local measurements.
No automatic file deletion policy is introduced in this work order.
UTC timestamps, drive letters, and environment counts remain visible metadata.

## Validation boundary

Phase 1 release remains NO-GO. This branch does not authorize production deployment.
Windows 10, disposable VM, GUI interaction, and independent PerfView comparisons remain NOT TESTED.
Elevated kernel ETW execution remains BLOCKED on the available nonadmin host.
No production trust gate, updater, vault writer, deletion engine, or user settings changed.

## Final source validation

Validated source: 68a220739bd0249ec0b6ba3df765baf7f0802489.
Validated source tree: e361aa65796301fead03635078e13d6987fe9335.
Branch: codex/pccare-performance-diagnostics-foundation.
Local Release tests: 805 passed, 0 failed, 111 skipped, 916 total.
New performance tests: 36 passed, 0 failed, 0 skipped. These are included in the application total.
Preserved updater tests: 128 passed. Separate SecurityLab tests: 36 passed.
The baseline had 769 passed and 111 skipped. The increase is 36 performance cases.
See GITHUB_RESULT.txt for the final evidence commit and Windows CI status.
Phase 1 remains NO-GO. Real-world causal accuracy has not been independently measured.

## Windows CI execution

CI run [38081909285](https://github.com/scarlel96-design/PC-Care/actions/runs/38081909285) completed successfully for source 68a220739bd0249ec0b6ba3df765baf7f0802489.
The hosted Windows runner reported elevated=true. It received 141992 numeric ETW records.
Its combined event-loss count was 27304. Zero newly owned ETW sessions remained after collection.
The snapshot reported no ETW startup, processing, loss-query, or cleanup error.
The capture lasted 30.064777 seconds with 29 polling samples.
The probe used 1.000444% whole-machine CPU and 59.468750 MiB sampled peak working set.
The nonadmin local host remains unable to execute kernel ETW. Its fallback measurement was verified separately.
The hosted runner smoke is not a disposable-VM UAC, reboot, GUI, or independent PerfView validation.
Event delivery and cleanup do not establish accurate causal attribution.
Event loss occurred. ETW event totals and per-process contributions are incomplete.
