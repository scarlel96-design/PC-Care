# WO25 — History state mapping

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

| Observed condition | Startup result | Verified | Review |
|---|---|---|---|
| Empty installation; no transactions | FreshInstallation | false | false |
| Existing unattested installation | LegacyUnattestedInstallation | false | false |
| Signed enrollment without committed update history | SignedInstallationVerified | true | false |
| Continuous authenticated retained history and matching files | SignedHistoryVerified | true | false |
| Missing retained declaration | InstallationHistoryIncomplete | false | true |
| Signature, scope, chain, content or identity mismatch | InstallationHistoryMismatch | false | true |
| Pending transaction | Existing pending/recovery classification | false | true |
| Unlisted executable after signed verification | ManualReviewRequired | false | true |
| Archived schema 1 terminal receipt | Legacy or existing migration-review result | false | state dependent |

New enum members follow all existing members. Existing ordinal values remain unchanged.
Schema 1 journals do not become signed history authority.
Terminal receipt validation binds the original record and directory identities.
A schema 1 restored record lacks a file ID.
Its read-only check therefore validates length and hash without inventing an empty identity.
Schema 2 retains mandatory identity validation.
Changed restored legacy bytes still require review.
An approved subsequent signed update supplies new authority.

The history reader validates package IDs, transaction IDs, root identity, declaration signatures, versions and file scope.
It requires a continuous signed version chain and a pointer to its terminal transaction.
It retains read leases through verification.
Scanning is bounded to 4,096 transaction directories, 10,000 installation files and 32 directory levels.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

The product coordinator preserves its existing Fresh and Legacy shell behavior.
The qualification launcher still requires Verified before launch.
It does not automatically enroll or launch an unattested installation.


## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.
