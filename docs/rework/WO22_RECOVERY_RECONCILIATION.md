# WO22 — Recovery reconciliation

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Read-only query

The query binds RequestId, request file identity, request hash and transaction ID.
It verifies the protected receipt scope and retained schema 1 journal.
It validates the current schema 2 journal independently.
It verifies the original package signature at the recorded approval time.
It compares backup identity, original signed hash, current identity, security and content.
Restoration completion requires every original file and Restored flag.
A missing path does not establish successful restoration.

## Interrupted operation

Before schema 2 promotion, a consumed request requires new operator review.
After promotion, the existing recovery engine controls resumption.
The query never grants a new file-change authority.
Other pending transactions block advice until ordering is resolved.
Corrupt current evidence does not become success through an older fallback.

## Process termination matrix

Twelve approval checkpoints include request verification, operator confirmation, reservation creation and reservation Flush.
They also include promotion, first and last restoration, journal completion and result return.
Four enrollment checkpoints cover preparation, Flush and activation.
Each terminated worker is followed by a separate-process query.
The matrix authenticates complete synthetic v5 extraction after recovery.
Read `RECOVERY_INTERRUPTION_RESULT.json` for actual commands, exit codes and bytes.
Process termination does not qualify physical power-loss durability.

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.

## Actual approval checkpoint results

| Checkpoint | First state code | Final verified | Kill exit |
|---|---|---|---|
| RequestVerified | APPROVAL-NOT-RESERVED | true | -1 |
| BeforeOperatorConfirmation | APPROVAL-NOT-RESERVED | true | -1 |
| AfterOperatorConfirmation | APPROVAL-NOT-RESERVED | true | -1 |
| ApprovalCreated | LEGACY-QUERY-REVIEW-REQUIRED | true | -1 |
| ApprovalFlushed | APPROVAL-CONSUMED-NEW-REQUEST-REQUIRED | true | -1 |
| BeforePromotion | APPROVAL-CONSUMED-NEW-REQUEST-REQUIRED | true | -1 |
| AfterPromotion | RESTORATION-REQUERY-RESUME-REQUIRED | true | -1 |
| AfterFileRestore | RESTORATION-REQUERY-RESUME-REQUIRED | true | -1 |
| AfterLastFileRestore | RESTORATION-REQUERY-RESUME-REQUIRED | true | -1 |
| BeforeRestoredJournal | RESTORATION-REQUERY-RESUME-REQUIRED | true | -1 |
| AfterRestoredJournal | RESTORATION-BYTES-VERIFIED | true | -1 |
| BeforeResultReturn | RESTORATION-BYTES-VERIFIED | true | -1 |
