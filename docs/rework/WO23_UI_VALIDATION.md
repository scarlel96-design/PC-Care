# WO23 — UI validation

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Implemented behavior
Reuse UpdateStatusViewModel and the existing recovery window.
The progress gate invalidates callbacks after completion and at the next BeginWork.
Both download and installation progress handlers check the captured operation generation.
Each service phase gets a new generation.
Terminal UI closures invalidate reports before changing their message.
Cancellation still clears preparation state and presents the existing Korean cancellation message.
The existing approval result query remains read-only.

## Verified scope
Unit tests execute the production UpdateProgressGate type.
Source checks verify cancel, recovery query and service connections.
The existing WinUI build verifies compilation.
These checks do not execute a product window or UI Automation interaction.

## Required real-environment checks
Use a disposable guest and an independent test SID.
Verify navigation, cancellation, retry, recovery queries, keyboard focus, high DPI and screen-reader behavior.
Verify delayed reports during repeated operations.
Close the screen-control session immediately after a permitted interactive test.
No screen-control session was started for the source checks.
Read `evidence/wo23/UI_TEST_RESULT.txt`.
