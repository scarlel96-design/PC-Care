# WO-14 v5 compatibility

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL.

| Format or path | Result | Evidence |
|---|---|---|
| Healthy LabFast v5, current legacy reader route | PASS | Two complete exports, source file hashes unchanged |
| Explicit synthetic v6 candidate | PASS | Reopened by a separate process; both content hashes match |
| Existing unprotected v5 MetadataReady | PRESERVED_BUT_LOCKED | Normal reader exit 4 |
| Copied MetadataReady read-only salvage | PASS for content | Three authenticated exports; commit history unqualified |
| Archival unmodified old program | NOT TESTED | No historical executable was executed |
| General production KDF profiles | NOT TESTED | Transition accepts only LabFast 1/65536/1 |
| Legacy spd-vault and v4 | Regression only | Existing tests retained; no transition added |
| AV3 production migration | BLOCKED | Six approval gates remain false |

The JSON field `oldReader` identifies the new probe using its legacy reader route.
It does not identify an archival old executable.
The source preserves its header, metadata, objects, journal, commit, recovery slots, and folder paths.
The source remains v5. The candidate uses the explicit v6 format identifier and generation directory.
The transition preserves VMK, DEK, VaultId, and encrypted object bytes.
It does not rekey content. The preserved v5 container retains the same key material.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
