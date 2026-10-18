# WO25 — Cross-volume refusal and interruption

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

The new harness requires actual different volume IDs.
It creates a fresh marked installation on the disposable hosted runner's system volume.
It keeps transaction staging under the runner temporary directory.
It records both IDs before the test.
It never fabricates a second volume using path strings alone.

The matrix covers normal refusal, termination before volume preflight, and termination after refusal.
Each case checks the original SHA-256, absence of the new file and zero transaction directories.
Reopening does not automatically resume a destructive operation.
Two same-volume termination cases retain the existing publication rollback tests.
They terminate before and after the new-file rename.
The parent then invokes explicit recovery and compares original bytes.

The local host run marks the hosted cross-volume trials NOT TESTED.
The CI evidence supplies the actual hosted result.
Options A and B are not implemented. Their copy, flush and secondary-publication checkpoints remain NOT TESTED.
Process termination does not establish reboot or physical power-loss durability.

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
