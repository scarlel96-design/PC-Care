# WO23 — Journal durability

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Preserved boundaries
Keep every journal checkpoint and Flush call.
Keep the bounded JSON format, duplicate-property rejection and directory identity checks.
Keep CreateNew temporary writes and current/previous journal replacement.
Semantic journal rejection does not select an older record.

## Commit ordering
The signed declaration and installation pointer now precede final journal commit.
Before final commit, recovery treats the transaction as incomplete.
After commit, a fresh process verifies declaration authority and installed file bytes.
The journal does not independently authenticate arbitrary installed content.
The existing signed package, ACL and handle boundaries remain mandatory.

## Validation
Terminate worker processes at declaration creation, partial write, Flush and pointer publication checkpoints.
Terminate workers before and after final journal commit.
Query the state from a new process.
Compare restored bytes and authenticated vault extraction hashes.
Read `evidence/wo23/COMMIT_CRASH_RESULT.json`.

## Limits
Process termination does not simulate a VM power cut or storage power loss.
Hardware durability and independent administrator qualification remain NOT TESTED.
