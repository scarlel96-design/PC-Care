# WO25 — Installation trust integration

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

## Implemented behavior

`UpdateRecoveryCoordinator` and `InstallationLauncher` now call `InspectStartupTrust`.
The existing current-declaration API remains compatible.
The new startup policy checks pending transactions before trusted history.
It authenticates retained declarations and validates their active files.
`Apply` checks the startup policy before acquiring its exclusive lock.
It checks retained history again under the lock.
This order prevents a self-created `RecoveryInProgress` result.

The original startup check validated only the current declaration.
An older disjoint file could therefore escape that declaration's scope.
The new check reconstructs the last signed declaration for each retained path.
An authenticated enrollment contributes its original paths.
Undeclared executables and DLLs require manual review after signed installation verification.
Ordinary data files remain preserved and unverified.

## Limits

This managed launcher does not provide native pre-CLR protection.
The loader can load managed dependencies before the app policy executes.
Protected installation ACLs remain part of the trust boundary.
History verification does not provide an independent freshness anchor.
Deletion of an entire historical prefix cannot always be detected without independent trusted state.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

The qualification launcher retains its existing Verified requirement.
Fresh and Legacy compatibility applies to the existing product coordinator path.
Deployment of this launcher as the product entry point remains unqualified.


## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.
