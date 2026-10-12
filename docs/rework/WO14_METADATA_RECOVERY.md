# WO-14 metadata recovery

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL.

| Test | Normal reader | Verified content | Commit qualification |
|---|---|---|---|
| Original interrupted v5 | Exit 4, PRESERVED_BUT_LOCKED | Normal recovery failed | Unqualified |
| Copied v5 salvage | ReadOnlyRecovery | All three entries match original bytes | Unqualified |
| Explicitly converted v6 interruption | RestoredPreviousGeneration, exit 0 | Both prior entries match | Authenticated previous generation |

The original v5 file manifest remains identical before and after salvage.
The recovery tool was terminated and retried with a new workspace.
The retried export again verified all expected bytes.

The synthetic salvage exception requires a v5 authenticated header and matching authenticated metadata generation.
It requires read-only opening and the internal synthetic salvage operation.
Normal production unlocking still rejects the reproduced mismatch.
Metadata AEAD tampering fails closed.
The tool does not rewrite a commit from journal statements.

This is authenticated content recovery, not proof of transaction completion.
The original locked container was not reclassified as normally restored.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
