# WO-09 GUI validation

Status: PARTIAL.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
Validated source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Code and headless validation

The existing WinUI panel style and keyboard-compatible controls are retained.
A recovery-plan panel displays evidence, Unknown effects, consent requirements, and preview preconditions.
DiagnosticLifecycle permits one collector until its cleanup finishes.
Stop requests cancellation. Restart waits for cleanup.
Navigation invalidates old progress and result callbacks.
A stale completion cannot release a newer collector lease.
Six headless lifecycle tests pass.
Release XAML compilation passes.

## Live GUI matrix

| Scenario | Status |
|---|---|
| Screen entry and Start/Stop | NOT TESTED |
| Immediate restart and navigation cancellation | NOT TESTED live; PASS headless lifecycle |
| Window close and resize | NOT TESTED |
| Repeated collection | NOT TESTED live; PASS headless lifecycle |
| Error and loss presentation | NOT TESTED live |
| Keyboard and screen reader | NOT TESTED |
| High DPI and reduced motion | NOT TESTED |
| High-load responsiveness | NOT TESTED |

Native GUI automation was not available in this session.
No live screen or accessibility result is inferred from compilation.
Use the final Release build on an isolated interactive Windows desktop.
Record screenshots, keyboard reachability, cancellation latency, and assistive-technology output.


## Evidence and restoration

Read `evidence/wo09/VERIFICATION.txt` for commands, inputs, outputs, and source-copy restoration.
Read `evidence/wo09/GITHUB_RESULT.txt` for the publication commit and CI source.
Use a reviewed Git revert to restore repository changes.
The source-copy rollback does not test an installation transaction.

## Pending live validation procedure

1. Launch a reviewed Release test installation with synthetic configuration.
2. Open the performance diagnostic page.
3. Start a 30-second read-only collection.
4. Stop collection during sampling.
5. Start collection after cleanup completes.
6. Navigate away during collection.
7. Return and check stale-result rejection.
8. Repeat collection and close the window during sampling.
9. Resize the window and test high-DPI layouts.
10. Navigate controls with the keyboard.
11. Read status, limitations, and plans with a screen reader.
12. Check reduced-motion behavior and load responsiveness.

Record screenshots, timestamps, cleanup status, and observed output.
Record failures without converting headless test success into live GUI success.
