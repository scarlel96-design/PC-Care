# WO22 — Cancellation safety

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Cancellation boundary

Apply accepts CancellationToken during package verification, package copy, extraction and backup preparation.
It checks cancellation before the first installation mutation.
Preparation cancellation preserves installed files and records `CANCELLED-PREPARATION`.
A failed cancellation record returns review rather than success.

After installation mutation begins, cancellation is deferred.
The engine completes a verified commit or runs its existing rollback path.
Recovery accepts cancellation before starting. It does not abandon an active restore.
`WasCancellationRequested`, `AnyMutationStarted` and `CancellationDisposition` describe separate observations.
A cancellation request does not imply that no bytes changed.
Rejected recovery before file writes does not report a mutation.
Rollback records mutation when it starts a copy or handle-bound deletion.

## Product connection

UpdateInstallerService uses asynchronous inspection with the supplied token.
UpdateStatusViewModel creates a linked preparation token.
The cancel button calls the actual preparation cancellation method.
Cancellation is disabled before elevated handoff.
The cancellation handler clears pending data and resets progress to zero.
Interactive ordering of queued progress messages remains NOT TESTED.
Indeterminate progress is used when no verified percentage exists.
Read `CANCELLATION_RESULT.json` for preparation and deferred-cancellation results.

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.
