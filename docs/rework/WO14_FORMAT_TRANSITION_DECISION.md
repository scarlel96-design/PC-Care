# WO-14 format transition decision

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PASS for selecting a test strategy; PARTIAL for product qualification.

| Criterion | A: in-place v5 protection | B: separate candidate |
|---|---|---|
| Existing format | Requires careful old-reader compatibility | Original v5 bytes remain unchanged |
| Failure rollback | Requires validated multi-file restoration | Reopen preserved original |
| Interrupted commit | Requires new v5 protocol | Reuses WO-13 v6 generations |
| Recovery keys | Must authenticate historical Used state | Issue fresh authenticated candidate codes |
| Storage | Smaller potential duplication | Two additional copies plus generation overhead |
| Complexity | Modifies existing live format | Isolates changes in a test workspace |
| Old executable interoperability | Unqualified | Original format preserved; executable trial pending |
| Security boundary | Existing writer and migration approvals required | Synthetic-only API and explicit consent |

Strategy B was selected and implemented.
The implementation copies the complete encrypted container and verifies decrypted content.
It then creates authenticated generations in the candidate.
It does not re-encrypt each object or create an independent key hierarchy.
The retained original therefore remains part of the key-protection threat model.

The fixture marker prevents accidental use. It is not a trusted production authorization mechanism.
No production transition button or automatic migration was enabled.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
