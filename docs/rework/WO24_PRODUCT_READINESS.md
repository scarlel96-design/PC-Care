# WO24 — Product readiness

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

WO24 remains PARTIAL.
Phase 1 remains NO-GO.
The isolated execution harness implementation can be prepared while guest qualification remains open.
Signed transition, independent SID, interactive UAC, privileged legacy restore and reboot remain P0 OPEN.
Product GUI and accessibility remain NOT TESTED.
All six AV3 production approval flags remain false.
Keep StorageCare production cleanup, folder deletion and risky v4/v5 password changes blocked.
Do not merge main or create an official release.

Installation-wide coverage in production startup remains OPEN.
The existing current-declaration API can omit older disjoint paths.
The new read-only qualifier detects that case, but production startup integration was not enabled.

| Priority | Requirement | Current disposition |
|---|---|---|
| P0 | Signed deployed Bridge transition | VM and isolated publisher execution required |
| P0 | Independent SID and UAC boundaries | Separate standard-user and administrator session required |
| P0 | Privileged schema 1 restoration | Actual approved administrator session required |
| P0 | Reboot recovery and guest rollback | Observed checkpoint and actual guest restart required |
| P0 | Full retained-history coverage at product startup | New read-only qualifier exists; startup integration remains open |
| P1 | Actual product GUI and accessibility | Interactive isolated profile required |
| P1 | Replacement IOException root cause | Verified rollback preserved; exact failure cause remains unknown |
| P1 | Strict-ACL performance and low-end suitability | One CI trial is insufficient for comparative qualification |
| P1 | Guest certificate cleanup | Mock error contract tested; actual guest lifecycle remains open |

The source-copy rollback proves only restoration of the changed signing script copy.
Synthetic installation rollback and authenticated vault extraction have separate evidence.
No observation comparator grants execution authority.
No independent freshness or whole-machine rollback defense was added.

## Volume boundary

The protected staging root and installation must use the same volume for new-file publication.
The failed CI trial used a D: installation and the default protected staging path.
Its exception and resolved staging root were not retained.
The cross-volume explanation remains a source-supported hypothesis for that failed run.
The existing `PublishOpenedFile` guard rejects that condition before the rename.
The engine reported rollback, but that run did not independently verify rollback bytes.
The corrected normal trial uses a new fixture on the staging volume.
A source-preservation regression verifies rejection without creating the foreign destination.
The test does not establish complete cross-volume update support.
The earlier WO23 replacement IOException remains a separate unresolved failure.

Retained-history startup integration also requires explicit fresh and legacy-unattested state mapping.
The new outer history fallback must not become an automatic repair instruction.

The prepared deployment tool requires independently validated complete build inputs.
Its verifier EXE hash check alone does not bind every dependency DLL.

## Final regression

Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.
Windows CI: [39015974010 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39015974010).
App: 1,376 PASS / 0 FAIL / 111 SKIP, local and CI.
SecurityLab: 36 PASS / 0 FAIL / 0 SKIP, separate from the app.
All 111 baseline skip names remain unchanged.
New WO24 cases: 8 Windows synthetic filesystem cases and 12 comparator cases.
Restore and Release report zero warnings and zero errors.
Actual guest deployment, UAC, privileged restore, reboot and product GUI remain NOT TESTED.
