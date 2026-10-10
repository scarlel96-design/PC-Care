# WO-07 UI integration

Status: PARTIAL. Release XAML compilation and adapter tests passed.
Interactive GUI validation remains NOT TESTED.

## Entry and behavior

Open Advanced tools, Program status, and Performance measurement.
The application also recognizes the PerformanceDiagnosticsPage navigation target.
Use the existing SpdMacPagePadding, MacWindowPanelStyle, and muted text tokens.
Display live CPU, available memory, and disk latency during the default 30-second capture.
Use Start and Stop buttons with standard keyboard control behavior.
Label unavailable measurements Unknown.
Display errors, sample count, elapsed duration, event loss, evidence, and confidence.
Label intentionally intensive work through the workload checkbox.
Cancel collection when the page unloads.
Reject stale progress callbacks after navigation.

## Implementation boundaries

Run collection and ETW processing off the UI thread.
Update text and progress at the polling frequency.
Do not add custom animations, charts, or a new motion subsystem.
Standard controls preserve the existing accessibility and reduced-motion behavior.
CareFindingQualityGate receives observation-only findings through an adapter.
CanAutoApply remains false. TargetPath remains null.
No cleanup or repair entry point receives these findings automatically.

## Remaining usability work

Test navigation, cancellation, keyboard order, screen-reader output, high DPI, and narrow-window layout interactively.
Translate technical evidence categories and typed error codes for the product UI.
The current evidence text preserves technical identifiers for review.
Qualify a chart library only when charts add clear diagnostic value.

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
