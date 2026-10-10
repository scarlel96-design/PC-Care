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
