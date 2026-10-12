# WO-14 v5 root cause

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL.

## Reproduced failure

The baseline probe executed three termination scenarios with separate reader processes.
Two readers verified all original bytes. One v5 MetadataReady reader remained locked.
The previous WO-13 failure remains a failure for the unchanged container.

The v5 writer overwrites metadata before activating its new commit.
The new authenticated header and metadata can share a generation while the activation marker still identifies an older generation.
The old metadata can already be lost. Header backups do not reconstruct that metadata.
The normal reader correctly rejects this mismatch.

## Implemented response

The source container stays unchanged.
An explicit synthetic transition creates two independently verified copies.
Healthy content enters the existing authenticated v6 generation protocol in the candidate copy.
Damaged MetadataReady content can enter a controlled read-only salvage path in a copy.
The salvage path verifies header authentication, metadata authentication, generation agreement, each chunk, size, and whole-content SHA-256.
It does not rewrite the activation marker or qualify commit history.

## Limits

This work does not repair every past v5 failure.
It does not make the default v5 writer crash-safe.
It does not infer a lost metadata generation from the unauthenticated journal.
Physical power-loss durability remains NOT TESTED.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
