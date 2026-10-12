# WO-14 recovery code integrity

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL.

## Current guarantees

The existing v5 Used field lacks authentication.
The transition does not promote historical Used bits into trusted evidence.
The candidate receives ten fresh v6 recovery codes.
The original v5 recovery store remains byte-identical.
The existing v6 store authenticates its state with a VMK-derived HMAC.
Used-bit tampering is rejected. Password access still verifies content.
The existing code reserves the vault session before code consumption.
It serializes store updates and uses flushed replacement records.
WO-13 regression keeps session-conflict, tamper, and four recovery-store crash tests.

## Unclosed delivery boundary

Persistent consumption can complete before the caller receives success.
A crash at this boundary can consume the code without a delivered session result.
The code is not reset automatically.
Password access and unused codes remain recovery options in the tested fixture.
This is at-most-once consumption, not exactly-once user delivery.

## Follow-up design

Use a durable operation identifier and authenticated consumption receipt.
Bind the receipt to the vault identity and session acquisition.
Allow receipt inspection after restart without resetting the Used state.
Test reservation, persistence, delivery, and restart as separate boundaries.
An external freshness anchor is also required for replay-resistant receipts.
This receipt protocol was evaluated but not implemented in WO-14.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
