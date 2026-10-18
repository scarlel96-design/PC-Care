# WO25 — Replacement exception analysis

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

## Recorded diagnostics

`UpdateFailureDiagnostic` records exception type, HRESULT, native Win32 code, phase and last journal stage.
It records bound target identity, volume IDs, mutation status and whether strict ACL checks were required.
It records the existing access and sharing contract.
It does not save raw exception paths or file contents.
Known path-free machine error codes remain available to existing callers.

The harness holds real read, write and exclusive handles in separate processes.
Conflicting access produces Win32 32 before installation mutation.
A read-only synthetic target produces Win32 5 before mutation.
The final result retains diagnostics through rollback.
Existing tests retain both primary, rollback and journal errors.

Microsoft documents incompatible sharing requests in [Creating and Opening Files](https://learn.microsoft.com/en-us/windows/win32/fileio/creating-and-opening-files).
These controlled conflicts do not identify an antivirus process as the historical cause.
The original WO23 IOException remains UNKNOWN.
DeletePending is recorded as NOT OBSERVED because this change does not query that native field.
No blanket IOException retry was added.
Actual disk-full and new independent ACL-denial fault injection remain NOT TESTED in WO25.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

The legacy Failure field retains existing rollback and journal message behavior.
Review those unstructured messages before external export.
Only the new structured Diagnostic contract excludes raw paths by construction.


## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.
