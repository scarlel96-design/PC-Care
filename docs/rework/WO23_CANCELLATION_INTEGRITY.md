# WO23 — Cancellation integrity

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Safe boundary
Cancellation remains available during package verification, extraction and backup preparation.
A preparation cancellation writes the existing terminal preparation-cancelled journal.
A journal-write failure remains a review outcome.
Once target mutation starts, the engine completes or rolls back before returning.
Commit and restoration do not abandon files when a cancellation token changes.

## New tests
Cancel a retained-handle preparation copy after its first buffer write.
Verify unchanged source bytes and an explicitly incomplete preparation destination.
Cancel extraction and backup preparation through the engine.
Run a separate normal update afterward.
Verify the subsequent signed installation.
These tests distinguish the copy primitive from engine-level cancellation.

## UI result
Invalidate queued progress reports when an operation ends.
Invalidate them again when another operation starts.
A late callback cannot replace a cancellation or completion message from another operation.
Actual dispatcher interaction and GUI timing remain NOT TESTED.
Read `evidence/wo23/CANCELLATION_RESULT.json`.
