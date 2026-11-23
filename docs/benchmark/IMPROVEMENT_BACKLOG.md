# WO28 - Ordered improvement backlog

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

All items reuse existing PC-Care services. Expected benefits are qualitative. No speed or accuracy gain is measured by this backlog.

| Order / ID | Work | Existing integration point | Acceptance evidence | Risk / release gate |
|---|---|---|---|---|
| P0 B01 | Inventory all residual files and terminate read-only reconciliation | `RecoveryReconciliation`, WO27 probe | Before/after relative-object inventory; content hashes; fresh-process result; zero writes during inspection | Unknown content and backups remain preserved. |
| P0 B02 | Qualify actual signed deployment and privileged recovery | Bridge, helper, attestation | Disposable VM snapshot, isolated SID, real signatures, UAC accept/cancel, reboot and full v5 extraction | VM/certificate/UAC shortages remain separate. |
| P0 B03 | Keep recovery state and content integrity separate in UI | Existing ViewModel/window/backend | State-to-message unit contract plus actual keyboard/UIA/reader flow | Actual GUI is required for GUI PASS. |
| P1 B04 | Explain recovery code consumption and backup credential history | Trusted receipts, v5 read-only salvage UI | Interrupted/acknowledged/stale cases; no code or key in exports | v4/v5 password change remains blocked. |
| P1 B05 | Profile history allocation sources before optimizing | `InspectInstallationHistory` | Matched ABBA runs; CPU/allocations/handles; adversarial file replacement tests | Retain all signatures/hash/ID/ACL checks. |
| P1 B06 | Add reviewed read-only cache providers | `CleanupAllowlist`, inventory, protection registry | Official path contract; synthetic ownership/link/protected-root tests; preview-only output | Production `CanExecute=false`. |
| P1 B07 | Present deletion scope and audit completeness | Existing preflight/result/journal | Cancel-before/after mutation; audit failure; unknown outcome; media explanation | Folder execution and physical claims stay blocked. |
| P1 B08 | Define settings-only backup scope | Existing settings service and updater | Schema/version checks, unknown fields, conflict preservation, crash copies | No automatic overwrite of unknown state. |
| P1 B09 | Add cross-volume staged-copy journal prototype | Existing A/B probe | Interruption before/during/after copy; hash/ID/ACL recheck; collision and restart results | Experimental only; production C refusal stays. |
| P2 B10 | Audit feature lifecycle consistency | Existing feature services | Cancel/navigation/Dispose/restart contracts; event subscription accounting | No new generic workflow or plugin engine without need. |
| P2 B11 | Evaluate reusable libraries | Existing adapters | Exact license, version, compatibility and regression evidence | No package adoption from popularity alone. |

## Excluded shortcuts

Do not replace authenticated generation choice with timestamps. Do not import executable cleaner rules. Do not replace AEAD with disk-sector encryption. Do not add SDelete as an automatic cleanup engine. Do not treat config restoration as whole-system rollback.

## External qualification

Independent Witness evaluation remains DAYBREAK_DEFERRED where special approval is required. Standard UI and ordinary file tests remain independent work. Physical device and power-loss tests require separate equipment evidence.
