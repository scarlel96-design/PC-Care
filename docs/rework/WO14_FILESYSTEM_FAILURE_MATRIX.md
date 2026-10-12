# WO-14 filesystem failure matrix

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL.

| Scenario | Status | Actual evidence |
|---|---|---|
| Snapshot file write or replacement | PASS | Retained handles reject writes and File.Move |
| Source directory replacement | PASS | Directory pins reject movement |
| Concurrent source inventory addition | PASS | Final inventory check rejects changed set; new file preserved |
| Hardlinked header | PASS | Actual Windows hardlink rejected before workspace creation |
| Locked header | PASS | FileShare.None causes safe rejection before copying |
| Cancellation | PASS | Original source hashes preserved |
| Existing workspace/name collision | PASS | Rejected; workspace not reused |
| Metadata authentication tamper | PASS | Transition fails closed |
| Transition child termination | PASS | Ten boundaries; original and retried candidate bytes verified |
| Recovery child termination | PASS | Salvage retry verifies all expected bytes |
| Converted password child termination | PASS | Ten boundaries; one valid password at each boundary |
| Same-instance Import versus Lock | Regression PASS | WO-13 barrier test retained |
| Two-process session conflict | Regression coverage | Recovery code not consumed on conflict |
| Concurrent Create, Import/PasswordChange, Export/Lock, Recovery/Dispose | NOT TESTED as complete matrix | Existing serialization does not qualify all pairs |
| Partial journal, invalid states, writer lock | Regression coverage | WO-13 retained tests |
| Real disk-full during copy or commit | NOT TESTED | Free-space preflight only |
| Actual ACL denial under a separate user | NOT TESTED | No independent test account |
| Full symlink and junction replacement matrix | NOT TESTED | Boundary rejection reused; new complete attack matrix absent |
| Old authenticated generation injection | Reproduced risk | Historical activation accepted |
| Physical power loss | NOT TESTED | Process termination is different |

The tests never authorize object deletion from an unauthenticated journal.
Ambiguous files and partial workspaces remain preserved.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
