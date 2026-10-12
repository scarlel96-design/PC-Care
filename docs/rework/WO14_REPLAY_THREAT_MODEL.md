# WO-14 replay threat model

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL. Replay protection is UNMITIGATED.

| Experiment | Actual result | Interpretation |
|---|---|---|
| Duplicate code without replay | Rejected | Consumption enforced in current state |
| Authenticated historical recovery store replay | Accepted | HMAC authenticates history, not freshness |
| Entire old container replay | Accepted | Local authenticated state can be rolled back |
| Historical activation/header/metadata combination | Accepted | Old generation authenticates and exports two entries |
| External freshness anchor | Absent | No anti-rollback claim |

The historical activation reader verified all returned file bytes.
Successful decryption does not make the selected generation fresh.
SHA-256 and HMAC cannot reject an authentic old snapshot without another trusted state.
DPAPI protection alone would not provide that external state.

## Candidate anchors

| Option | Benefit | Required qualification |
|---|---|---|
| Remote witness | State outside replayed container | Availability, authentication, outage recovery, privacy |
| TPM-backed monotonic policy | Hardware-bound state | Actual Windows support, reset, device loss, portability |
| Protected host state | Separate local directory | ACL, admin replay, backup semantics, device migration |

No anchor was fabricated or installed.
Replay remains a P0 production approval blocker.
Fresh codes avoid trusting old Used bits but do not solve historical candidate replay.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
