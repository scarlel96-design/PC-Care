# WO25 — Execution report

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

## Implemented changes

1. Connect retained signed-history checks to product startup, launcher and update preflight.
2. Preserve Fresh and Legacy compatibility without assigning verified trust.
3. Retain schema 1 length/hash observation without an invented empty file identity.
4. Reject cross-volume new-file publication before transaction creation and mutation.
5. Preserve native error diagnostics through failure and rollback.
6. Authenticate complete deployment and verifier input closures before execution.
7. Explain history states in Korean and perform refresh inspection outside the UI thread.

No third-party package version was upgraded.
The implementation reuses .NET cryptography, WindowsFileBoundary and the existing updater, signer and recovery engines.
Independent source-copy rollback and installation transaction recovery have separate evidence.

Final source SHA, test totals, CI URL and remote publication checks are recorded in `evidence/wo25/VERIFICATION.txt`.
Intermediate failures remain recorded in `INTERMEDIATE_RESULTS.json`.
Only the final frozen-source run establishes the reported passing regression.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.

## Measured final observations

The CI runner used two distinct volume IDs.
Three trials preserved the original hash, reported zero transactions, and left the new file absent.
Two trials terminated a separate process at the preflight or refusal checkpoint.
The child exit code was -1. The read-only verification exit code was 0.

Authenticated v5 extraction passed 28 reopen checks and 112 complete SHA-256 pairs.
The retained crash matrix contributed 26 checks and 104 pairs.
The normal update flow contributed two checks and eight pairs.

One strict-ACL sample processed 258 synthetic files in 43,891.1198 ms.
CPU time was 13,765.625 ms. Peak Working Set was 212,713,472 bytes.
Managed allocations were 843,023,728 bytes. Journal writes and Flush calls were both 779.
No performance improvement is established by this single sample.

The 111 existing skip names match the baseline exactly.
MSTest reports zero in Counters.notExecuted, but contains 111 NotExecuted result rows.
The evidence counts result rows and checks the total of 1,519.

The CI reports an action-runtime Node.js 20 deprecation annotation.
This is an infrastructure maintenance item. It is separate from compiler warnings.
