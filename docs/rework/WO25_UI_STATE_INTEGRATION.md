# WO25 — Product UI state integration

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

The actual recovery coordinator uses the new startup policy.
The recovery window maps history failures to Korean explanations.
Technical result codes remain available for diagnostics.
The refresh handler runs inspection outside the UI thread.
It disables the refresh button until inspection completes.
It preserves files and reports review requirements when inspection throws.
No invented progress percentage was added.

Existing administrator launch, approval query and recovery controls retain their service calls.
Starting a helper still does not mean recovery completed.
The normal shell remains subject to the coordinator result.
Normal Fresh and Legacy states remain unverified compatibility states.

Source-contract tests verify coordinator and launcher calls.
Korean message tests verify selected state descriptions.
These tests do not exercise an actual WinUI window.
Actual GUI, DPI, keyboard interaction, screenreader and reduced-motion tests remain NOT TESTED.
No screen-control session was started or reconnected for WO25.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

Cross-volume refusal text has unit-test coverage.
Actual delivery from an elevated helper to the product UI remains NOT TESTED.
A formatter alone does not prove that end-to-end notification path.


## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.
