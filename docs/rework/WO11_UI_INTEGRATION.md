# WO-11 — System care UI integration

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Implemented controls

The SystemCare page contains a bounded storage panel using existing design tokens.
It displays drive capacity, caller-available space, total free space, and measurement limits.
The candidate list supports multiple selection and wrapped descriptions.
Scan, Stop, and Dry Run controls expose stable UI Automation IDs.
The execution button remains disabled and explains the pending production qualification.
Unknown sizes display as unconfirmed. Missing measurements do not display as zero-sized normal results.

Collection runs on a worker task.
DiagnosticLifecycle rejects stale results after navigation.
Candidate updates are bounded to 512 rows.
Cancellation remains active until the current task completes.
No event stream or heavy animation was added.
The preview separates candidate bytes from unmeasured actual space gain.

## GUI validation status

Release XAML and solution build: PASS, zero warnings and errors.
GuiProbe storage-page lifecycle command: implemented.
Actual interactive GUI execution: NOT TESTED.
High DPI, narrow-window rendering, keyboard focus, screenreader behavior, and reduced motion: NOT TESTED.
Repeated GUI leak and high-load response tests: NOT TESTED.

The local app startup can write settings outside the synthetic fixture.
A disposable interactive profile was not provided.
The default GuiProbe result explicitly reports guiExecuted=false.
No XAML compilation result was promoted into a GUI PASS.

## Follow-up

Run GuiProbe --exercise-storage-page against an isolated interactive build.
Test selection and Dry Run with protected and changed candidates.
Review the legacy auto-apply wording with its blocked file-action states.
Measure layout, accessibility, and cancellation with actual rendered controls.
