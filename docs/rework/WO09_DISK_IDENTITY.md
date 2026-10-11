# WO-09 physical disk identity

Status: PARTIAL.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
Validated source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Read-only mapping

The qualification tool joins PhysicalDisk instance numbers to Get-Disk data.
It records partition counts and requires an unambiguous Windows storage UniqueId.
A private 32-byte key creates HMAC identifiers for published evidence.
The key and raw storage input are removed after the two read-only trials.
The key is test-scoped. Production persistence and rotation are not implemented.
Duplicate disk numbers, duplicate storage IDs, missing IDs, and _Total remain Unknown.
The HMAC excludes disk numbers, so synthetic renumbering retains the identity with the same key.
Live renumbering and hardware replacement remain NOT TESTED.

Local trials observed three devices with identical identifiers in both captures.
One device qualified as NVMe SSD. Two device types remained Unknown.
No SATA/HDD qualification is claimed for this host.
The existing application per-disk model still uses session counter identities.
This mapping tool does not silently promote those identifiers into persistent application identities.
Volume and partition counts do not prove complete topology mapping.
Device type does not determine normal latency or expected performance improvement.


## Evidence and restoration

Read `evidence/wo09/VERIFICATION.txt` for commands, inputs, outputs, and source-copy restoration.
Read `evidence/wo09/GITHUB_RESULT.txt` for the publication commit and CI source.
Use a reviewed Git revert to restore repository changes.
The source-copy rollback does not test an installation transaction.
