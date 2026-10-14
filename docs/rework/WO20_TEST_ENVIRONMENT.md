# WO20 — TEST ENVIRONMENT

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Baseline

WO19 CI reported 1230 PASS, 111 SKIP and zero failures.
The historical local scanner timeout remains an observed prior failure.
The fresh original scanner test passed without changing its existing time limits.
No disk I/O or background-process causality was measured for the historical timeout.

## Scanner implementation

CareFolderScanner now enumerates directories lazily instead of eagerly allocating arrays and parallel workers.
It bounds directory count and checks cancellation during enumeration.
Reparse-point query failure is treated as excluded.
The original timeout acceptance thresholds are unchanged.
Test telemetry records start/end, CPU, GC, threadpool count, elapsed time and file count.
No unmeasured speed improvement is claimed.

## Explicit fixture boundary

wo20-test-environment.ps1 now requires an absolute FixtureRoot with a wo20- name.
It rejects the repository product tree, Windows, ProgramFiles and existing reparse ancestors.
It sets RUNNER_TEMP explicitly. CI persists the validated path across later steps.
The first full local run used a protected PC-Care ancestor and returned 29 failures.
This was a fixture-location defect. The product protection policy correctly refused those targets.
The failed log and TRX remain evidence. No protection check was weakened.

PowerShell 7 worker-output handling and script-scoped record storage were also corrected.
The failed CI runs remain listed in GITHUB_RESULT.txt.

## Evidence and reproduction

Read `evidence/wo20/VERIFICATION.txt` for commands, inputs, exits and source provenance.
Read the named JSON evidence for literal process outputs and SHA-256 comparisons.
Use only synthetic fixtures. Keep private fixture passwords and DPAPI files outside published evidence.

## Restore procedure

Use `ROLLBACK.sh` only on an independent source copy named RESTORE_COPY.
Use the trusted updater recovery entry point for authenticated schema 2 transactions.
Review schema 1 plans with the signed original-version package and explicit consent.
Preserve files when identity, content, ACL, journal or package checks fail.
Source rollback does not restore installation data or encrypted vault contents.

## Final local build-path qualification

The solution's x64 configuration maps the app test project to bin/Release.
An x64 --no-build invocation instead uses bin/x64/Release.
That mismatch omitted one newly added test in the earlier local result.
Build the test project directly with -p:Platform=x64 before using --no-build.
Do not rebuild a shared test output directory while testhost holds its assemblies.
The lock failure is preserved as a failed build attempt.
The final focused and full results use a freshly built x64 test project.

Final local outcomes: `{"Passed": 1261, "NotExecuted": 111}`. New WO20 outcomes: `{"Passed": 31}`.

## CI harness failure interpretation

Three early CI runs failed while constructing a crash-worker record.
The log reported a null expression. It did not identify the null member independently.
Casting the empty output alone did not close the CI failure.
The final change uses a script-scoped record list and a dedicated worker-output variable.
The final CI and local PowerShell 7 matrix validate that combined change.
