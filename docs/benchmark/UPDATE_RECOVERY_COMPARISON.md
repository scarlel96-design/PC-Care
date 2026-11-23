# WO28 - Update and recovery comparison

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

## PowerToys references

General Settings documents update states, configuration backup, and settings restoration. This is a useful user-flow reference. It does not establish PC-Care's signed deployment, privilege or crash guarantees. [General Settings](https://learn.microsoft.com/en-us/windows/powertoys/general).

`SettingsBackupAndRestoreUtils.RestoreSettings` checks backup locations and merges or replaces selected JSON settings. Its companion policy lists included files, ignored fields and specific overwrite behavior. Settings restoration is narrower than executable installation rollback. [Utility](https://github.com/microsoft/PowerToys/blob/1b33221dfa19f30d71409eb550807883fb27283a/src/settings-ui/Settings.UI.Library/SettingsBackupAndRestoreUtils.cs); [policy](https://github.com/microsoft/PowerToys/blob/1b33221dfa19f30d71409eb550807883fb27283a/src/settings-ui/Settings.UI.Library/backup_restore_settings.json).

## PC-Care source comparison

| Area | Actual PC-Care evidence | Classification |
|---|---|---|
| Signed update and declaration | Existing `SignedPackage`, `InstallationAttestation` and protected transaction paths | 부분 구현 |
| Unknown current content | `RecoveryReconciliation` reports `UnknownContent`; automatic resume refuses it | 부분 구현 |
| Mutation results | `UpdateTransaction` separates apply and recovery mutation | 부분 구현 |
| Retained history | `InspectInstallationHistory` keeps journal/declaration/file evidence and shares directory pins | 부분 구현 |
| Settings-only recovery | No equivalent general settings recovery guarantee was established by inspected paths | 추가 근거 필요 |
| Actual privileged deployment | Independent SID, signed Bridge, UAC and reboot proof remains open | 개선 필요 |
| UI explanation | Existing `UpdateRecoveryWindow` and diagnostics require real interactive qualification | 개선 필요 |

## Continuation priorities

Retain unknown bytes and trusted backups after mid-copy interruption. Inventory all residual objects before and after read-only reconciliation. Terminate reconciliation itself in a separate synthetic process. Compare another fresh inspection with untouched hashes.

Profile history parsing and per-record allocations independently. Preserve signature, full hash, file identity, ACL and retained-handle boundaries. Change neither journal Flush semantics nor trust checks merely to reduce time.

Keep cross-volume A/B staging experimental. The production same-volume refusal remains. Adopt settings-backup ideas only through a scoped, authenticated, conflict-preserving PC-Care contract.
