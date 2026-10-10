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
