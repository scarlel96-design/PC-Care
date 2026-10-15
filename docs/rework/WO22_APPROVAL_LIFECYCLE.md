# WO22 — Approval lifecycle

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Problem and change

A consumed approval proves reservation. It does not prove restoration or caller acknowledgement.
`InspectLegacyApproval` reads the retained request, protected reservation, original package, journals, backups and current files.
It returns only evidence-backed states. It does not start or resume restoration.

## Evidence-backed transitions

| Observation | Result | Next action |
|---|---|---|
| Bound request without reservation | ApprovalRequested | Request a reviewed approval |
| Valid reservation with unchanged schema 1 | ApprovalReserved | Create a new reviewed request |
| Valid promoted journal awaiting restoration | RestorationInterrupted | Invoke existing recovery through its normal boundary |
| Recovery mutex held | RestorationInProgress | Wait and query again |
| Restored journal, restored flags and every authenticated original hash | RestorationCommitted | Display verified restoration |
| Missing, corrupt, conflicting or unknown evidence | RecoveryReviewRequired | Preserve data and review |

Request verification, scope verification and operator confirmation remain validated conditions.
They are not separately persisted decorative states.
`CallerAcknowledged` is null because durable evidence cannot prove response delivery.
A repeated RequestId returns `LEGACY-APPROVAL-CONSUMED; QUERY-RESULT` before any new mutation.
Concurrent reservation uses the existing CreateNew and Flush boundary.
A second pending or untrusted transaction blocks read-only resume advice.

## Authority

Production execution retains elevated SID, strict ACL and signed package requirements.
The request digest is a binding value. It is not a signed authorization token.
Synthetic consent callbacks do not prove administrator approval.

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.
