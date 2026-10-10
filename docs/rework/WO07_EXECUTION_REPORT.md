# WO-07 execution report

Status: PARTIAL. Phase 1 release remains NO-GO.

## Git identity

Branch: codex/pccare-performance-diagnostics-foundation.
Base commit: f793cbabcbf8beb4355a7538c28b1e26ad4517b3.
Base tree: e1ddfde08a5333e42630189991ed7a100f5b57a6.
Final source and remote identifiers appear in evidence/wo07/GITHUB_RESULT.txt.

## Actual implementation

Added a .NET 10 diagnostics library with TraceEvent 3.2.8 and CsWin32 0.3.358.
Added bounded read-only native, PDH, process, and optional ETW collection.
Added conservative persistence rules, explicit Unknown results, confidence inputs, and percentile summaries.
Added an observation-only adapter and a page inside the existing WinUI design system.
Added a CLI capture probe, synthetic scenarios, and Windows CI with existing security regression checks.
No change was made to the vault, deletion, updater, or recovery source implementation.

## Information unavailable from GitHub alone

The available machine runs Windows 11 Pro build 26200 with 24 logical processors and about 32 GiB physical memory.
The process token was nonadmin. Sandbox escalation changed process restrictions, not Windows administrator membership.
Initial sandbox test-host connection failed. An unrestricted retry completed the baseline.
PDH initially returned no counters in the restricted POC. Nonadmin unrestricted captures read real counters.
No disposable Windows VM or independent reference capture was supplied.
No UAC prompt, reboot, physical power-loss test, or installed-user application modification occurred.
The GUI compiled. Native GUI interaction was not performed.
The host was not reserved for idle measurements. Development and regression work ran during this task.
Capture JSON uses salted identifiers. It excludes executable paths, names, and personal file contents.
Evidence includes actual outputs and initial failures. It does not substitute a test plan for execution.

## Requirements still open

Elevated long-duration ETW collection and independent PerfView accuracy require further qualification.
Thermal sensors, processor-group support, ETL replay, startup impact, and service correlation require later development.
Process enumeration is capped and unordered. It can omit the actual largest resource consumer.
Aggregate disk screening needs per-device hardware calibration and independent workload validation.
User workload context is explicit. Normal high-load applications are not classified automatically.
Real GUI accessibility, low-spec responsiveness, and Windows 10 execution remain NOT TESTED.
No real-world accuracy percentage or performance recovery improvement is claimed.

## Source restoration

Use the rollback fixture script to restore only the independent package-manifest copy.
That test does not validate installation rollback or updater recovery.
For a full source reversal, create a separate branch and revert the WO-07 source commit.
Run the baseline regression before accepting that reversal.
Keep the WO-06 base commit available. Do not reset or overwrite unrelated user work.

## Next product work

Qualify elevated ETW in a disposable VM and compare independent PerfView captures.
Bind event attribution to process lifetimes before using it for actionable diagnoses.
Add workload matching and repeated-capture evidence before recommending repairs.
Calibrate disk rules per device and add startup/service attribution.
Implement user-approved recovery plans only in a separate work order.

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

## Completion criteria

| Requirement | Result | Evidence |
|---|---|---|
| Candidate comparison | PASS | Official releases, licenses, public advisory checks, transitive audit |
| Selected-library POC | PASS | Actual native/PDH capture and hosted kernel ETW delivery |
| Collection and normalization | PASS for measured Windows 11 paths | Nonadmin captures and elevated CI capture |
| Evidence rules | PASS for synthetic scenarios | 36 performance tests |
| Permission, cancellation, loss and errors | PASS for exercised boundaries | Fallback, observer exception, cancellation, queue overflow |
| Resource measurement | PASS for sampled fallback budget; PARTIAL overall | Repeated 30-second captures, 120-second capture, hosted ETW capture |
| Existing UI integration | PARTIAL | Release XAML build and adapter tests; GUI interaction NOT TESTED |
| Real causal accuracy | NOT TESTED | No independent PerfView or workload ground truth |
| Startup/service/thermal diagnosis | PARTIAL | Inventory and Unknown outputs; causal attribution remains open |
| Windows CI | PASS | Run 38081909285; 805 app tests, 111 skips, 36 separate SecurityLab tests |

WO-07 remains PARTIAL as a product qualification. The branch is ready for further engineering review.
The next work order can qualify event-loss reduction, process lifetimes, independent workloads, and interactive UI behavior.
Read-only observation does not authorize automatic remediation.
The CI runner reported a Node.js 20 action-runtime deprecation annotation.
The runner used Node.js 24. The CI build reported zero warnings and zero errors.
Upgrade workflow action versions during the next CI maintenance review.
