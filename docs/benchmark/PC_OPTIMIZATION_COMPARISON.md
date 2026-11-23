# WO28 - StorageCare and optimization comparison

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

## BleachBit findings

CleanerML describes cleaners with XML, operating-system predicates and search rules. It includes destructive and process-launch actions. Schema validation does not make a rule safe. [CleanerML](https://docs.bleachbit.org/cml/cleanerml/).

The documented workflow selects options, previews results, reviews them, then performs deletion. [Official README](https://github.com/bleachbit/bleachbit/blob/61f477afc2af11a982402294a319e741da4b97a0/README.md). Preferences include delete confirmation and exclusions that affect preview and cleaning. [Preferences](https://docs.bleachbit.org/doc/preferences/).

`CleanerML.handle_cleaner_option_warning` maps rule warnings. `Cleaner.get_commands`, `is_process_running`, and `whitelisted` separate rule definition, process checks and exclusions. These inspected methods are useful design references. They do not prove safe restoration or identical preview/execution targets. [Parser](https://github.com/bleachbit/bleachbit/blob/61f477afc2af11a982402294a319e741da4b97a0/bleachbit/CleanerML.py); [cleaner](https://github.com/bleachbit/bleachbit/blob/61f477afc2af11a982402294a319e741da4b97a0/bleachbit/Cleaner.py).

## PC-Care assessment

| Feature | Inspected PC-Care implementation | Classification |
|---|---|---|
| Bounded inventory | `StorageInventory.FileBudget=512`; exact identity and missing reasons | 부분 구현 |
| Read-only preview | `StorageInventory.DryRun`; `CleanupCandidate.CanExecute=false` | 부분 구현 |
| Protected stores | `StorageTargetPolicy.IsProtected` examines ancestors; registry covers vault/recovery/anchor/audit/key/update stores | 부분 구현 |
| Provider rules | `CleanupAllowlist.ReadOnlyProviders` currently exposes UserTemp observation | 개선 필요 |
| Reversible execution | Existing synthetic sandbox; production cleanup blocked | 부분 구현 |
| Space accounting | `StorageSpaceAccounting` separates logical, allocated, quarantined and deleted bytes | 부분 구현 |
| Speed improvement | No controlled cross-product before/after workload was run | 추가 근거 필요 |

The provider classification above is `개선 필요`. It identifies limited provider coverage, not permission to scan arbitrary roots.

## Independent implementation proposal

Define a small read-only provider schema. Include provider identity, OS applicability, official cache contract, warning text and protected-root exclusions. Reject commands, registry writes and unrestricted globs. Reuse `StorageInventory`, `StorageProtectionRegistry` and `WindowsFileBoundary`.

Preview must bind the selected object identities. Revalidate ownership, identity, content and parent directory before any future approved execution. Keep same-volume quarantine at zero verified freed bytes. Report unattributed volume changes separately.

Expected benefit: clearer coverage and review. Resource reduction and recovered-space accuracy require measurement. BleachBit rules are not imported in WO28.
