# WO-10 gui automation

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Implementation

The external GuiProbe uses Microsoft UI Automation.
The diagnostic XAML exposes Start, Stop, Status, and Plans automation IDs.
Status uses a polite live region. Controls receive meaningful Korean accessibility names.
The harness requires an explicit PID and --exercise-readonly-page.
It verifies the interactive session and main window before querying controls.
It performs two start/stop cycles and a keyboard-focus check with bounded waits.
It never activates an execution action.

## Actual result

The Release harness compiled with zero warnings and errors.
The no-argument run returned NOT TESTED, guiExecuted=false, exit 0.
No interactive application session was supplied in this run.
No installed product GUI was opened by the test.
Live Start, Stop, restart, navigation cancellation, and window closing remain NOT TESTED.
High DPI, narrow windows, screen reader, reduced motion, and high-load UI latency remain NOT TESTED.
XAML build and headless lifecycle regression do not change these statuses.

## Interactive procedure

1. Launch a dedicated test application with synthetic settings.
2. Open the performance diagnostics page.
3. Run GuiProbe --exercise-readonly-page with that process PID.
4. Record the actual JSON output and process exit code.
5. Test completed Dry Run and blocked-plan presentation manually.
6. Test navigation, window close, DPI, keyboard, reader, and reduced motion separately.

FlaUI remains a reviewed alternative. Live compatibility was not established.
The existing design tokens and planning-only product flow remain unchanged.
