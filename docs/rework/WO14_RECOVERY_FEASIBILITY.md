# WO-14 recovery feasibility

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL.

## Evidence classification

| Requested state | Observed meaning | Current coverage |
|---|---|---|
| HealthyV5 | Current v5 reader authenticates and exports all entries | PASS |
| InterruptedButRecoverable | A copied authenticated payload can be salvaged | One MetadataReady fixture |
| AuthenticatedPreviousStateAvailable | Converted v6 reader selects previous authenticated generation | PASS |
| PreservedButLocked | Normal v5 reader rejects commit mismatch | PASS for preservation, no normal recovery |
| MetadataAuthenticationFailed | AEAD modification rejects transition | Unit and file test |
| CommitGenerationMismatch | Header/metadata differ from active v5 commit | Reproduced |
| RecoveryMaterialInsufficient | Required material cannot establish content identity | General taxonomy remains PARTIAL |
| CorruptBlocked | Authentication failure does not authorize mutation | Retained tests |

The public result currently exposes transition statuses, not all requested diagnostic enum values.
An authenticated payload does not prove that its transaction was committed.
Read-only salvage reports that distinction explicitly.

## Recovery procedure

1. Preserve the original fixture and its manifest.
2. Select a new synthetic workspace.
3. Authenticate the copied header and metadata.
4. Verify every referenced object and content hash.
5. Export into the private workspace.
6. Compare every exported byte and relative path.
7. Keep commit history unqualified when activation evidence fails.

Never delete unreferenced ciphertext from journal evidence.
Missing historical metadata cannot be reconstructed from checksums alone.
The preserved locked fixture remains private and unchanged.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
