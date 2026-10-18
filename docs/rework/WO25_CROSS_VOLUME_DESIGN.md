# WO25 — Cross-volume design decision

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

| Option | Benefits | Required proof | Decision |
|---|---|---|---|
| A: protected staging on target volume | Same-volume publication | New ACL, path, ID, cleanup and interrupted-publication boundary | Deferred |
| B: secondary protected copy | Existing source staging remains intact | Full second-copy authentication, journal, space budget and recovery | Deferred |
| C: fail before mutation | Retains the existing publication boundary | Actual refusal and original-byte checks | Selected |

`Apply` verifies the signed package before the volume preflight.
If a package introduces a missing file, staging and installation must share the volume boundary.
The preflight compares pinned volume identities and path roots.
It returns `PUBLISH-SAME-VOLUME-REQUIRED` before creating a transaction or changing installation bytes.
The existing `PublishOpenedFile` check remains active.
No cross-volume rename or secondary-copy publication is enabled.
Copy and rename are not treated as one atomic operation.

Microsoft documents cross-volume behavior in [MoveFileExW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw).
The implementation retains its stronger same-volume handle-publication contract.

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
