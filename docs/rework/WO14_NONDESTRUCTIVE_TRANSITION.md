# WO-14 nondestructive transition

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PASS within the tested synthetic boundary.

## Implemented sequence

1. Require explicit test consent.
2. Validate the fixture marker and sibling workspace name.
3. Reject a reused workspace.
4. Pin source ancestors, directories, and file objects.
5. Reject reparse points and multi-link files.
6. Hash every source file.
7. Bound entries, bytes, and unauthenticated KDF inputs.
8. Check available space for two copies and reserve.
9. Copy into `preserved-v5` and `candidate-v6`.
10. Flush and verify copied files.
11. Authenticate all candidate content.
12. Generate fresh candidate recovery codes.
13. Commit through the existing v6 protocol.
14. Reopen and verify all candidate entries.
15. Verify source inventory and hashes again.

Limits: 10,000 entries; 2 GiB source bytes; LabFast v5 only.
Minimum free-space estimate: `2 * sourceBytes + 64 MiB`.
The estimate is a preflight reserve, not a disk-full guarantee.
The measured normal fixture contained 143,432 source bytes.
This produces a minimum preflight estimate of 67,395,728 bytes.
Both independent copies consume storage. A same-volume rename is not counted as backup.

Ten child-process termination points preserved all source file hashes.
Every original reader exported both original entries after termination.
Retry used a new workspace and verified the candidate again.
Incomplete candidate workspaces remained preserved for inspection.

The recovered fixture retains a nested Korean path and an empty file.
The raw evidence contains full per-file source manifests and content hashes.
No private keys or recovery codes appear in public evidence.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
