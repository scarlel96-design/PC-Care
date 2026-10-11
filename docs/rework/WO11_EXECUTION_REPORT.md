# WO-11 — Execution report

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Outcome

Implemented bounded storage metadata inventory and a SystemCare preview flow.
Extended the existing synthetic transaction with bounded payload sizes and separately approved final deletion.
Blocked legacy path-based file cleanup while production execution remains unqualified.
Preserved numeric Windows file errors for accurate lock reporting.
Extended Windows CI and the UI Automation harness.

## Local validation

| Test group | PASS | FAIL | SKIP |
|---|---:|---:|---:|
| Baseline application | 957 | 0 | 111 |
| Modified application | 1002 | 0 | 111 |
| New WO-11 cases, included above | 45 | 0 | 0 |
| Update regression, included above | 128 | 0 | 0 |
| Latest combined WO-11 and update rerun | 173 | 0 | 0 |
| SecurityLab, separate project | 36 | 0 | 0 |

Release solution build: zero warnings, zero errors, exit 0.
Six separate-process interruption recoveries restored matching original bytes.
Three link attacks were rejected.
Two irreversible deletion states retained review status and unchanged journals.
The 8 MiB fixture completed quarantine, restoration, and separate final deletion.
The user Temp metadata scan was incomplete at its explicit budget and made zero target mutations.

## Failures retained and resolved

The initial lock-classification test exposed loss of the native Windows error code.
WindowsFileBoundary now preserves the inner Win32Exception.
A stale test assembly initially repeated the old result.
A forced test rebuild produced 45 passing WO-11 tests.
The source-copy fixture also required a forced rebuild after Copy-Item preserved its timestamp.
The initial sandbox link test lacked native hardlink privileges.
The authorized isolated rerun passed its link and crash scenarios.
The first read-only probe invocation omitted Platform=x64 and could not find its binary.
The corrected command returned exit 0 and an explicitly incomplete inventory.
No failed command was counted as a passing test.

## Scope and provenance

The implementation modifies only source and generated test data.
The test executor is absent from the production dependency graph.
Update signing gates, AV3 Writer gates, and secure-delete execution remain unchanged.
No official release or main merge is part of this work.
See GITHUB_RESULT.txt for publication and Windows CI provenance.

## Windows Runner ownership correction

The first Windows CI run was 38130962723.
Release and 128 updater tests passed.
The performance group returned 224 PASS and 9 FAIL.
The elevated Runner assigned its default group owner to generated test files.
Production inventory correctly classified these files as UnknownOwnership.
Test fixtures now explicitly assign their candidate file to the current user SID.
The standalone probe also requires exactly one 2048-byte preview candidate.
Production ownership policy remains unchanged.
The corrected local WO-11 group returned 45 PASS, zero FAIL, and zero SKIP.
The corrected synthetic probe returned WO11_CLEANUP_PASS and exit 0.
The first local test retry stalled before output under the sandbox.
That process was interrupted with exit 1.
The IPC-enabled retry completed with exit 0.
No interrupted run was counted as PASS.
## Final Windows CI

CI run [38131365937](https://github.com/scarlel96-design/PC-Care/actions/runs/38131365937) completed successfully.
Validated implementation: `bec4c1bf977f65f5e8299150c8e0a48f72abf1ec`.
Validated tree: `467ab0d0e076323ffb929ebbada7b8cd3d367b36`.
The Windows Runner reported 1002 PASS, 111 SKIP, and zero FAIL in the full application group.
The 233 performance and 128 updater tests are subsets of that group.
The new WO-11 group contains 45 passing tests.
SecurityLab separately reported 36 PASS.
Release compilation reported zero warnings and zero errors.
Downloaded TRX records independently confirm these counts.
Six separate-process recoveries preserved the original SHA-256.
Three link inputs were rejected. Two irreversible states preserved their journals for review.
The 8 MiB roundtrip preserved its original SHA-256.
The CI GUI record reports `NOT TESTED` and `guiExecuted=false`.
See `evidence/wo11/CI_RESULT.txt` for exact commands and literal output.

## Publication provenance

Implementation and test-fixture correction commits were uploaded to the independent work branch.
The final publication commit adds reports and evidence only.
Its executable source matches the validated implementation commit.
Resolve its commit with the commands in `evidence/wo11/GITHUB_RESULT.txt`.
The final local receipt records exact local and remote HEAD values after upload.
WO-11 remains PARTIAL. Phase 1 remains NO-GO.
