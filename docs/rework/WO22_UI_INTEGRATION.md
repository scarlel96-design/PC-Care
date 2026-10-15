# WO22 — UI integration

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Service connection

UpdateRecoveryWindow reuses the existing recovery coordinator.
It previews relative target paths, lengths and classifications before privileged approval.
Its RequestId query calls the read-only approval inspection service.
Shared Korean messages explain reservation, interruption, verified restoration and manual review.
Caller acknowledgement remains unknown.
Unknown content and conflicting transactions remain review states.
No plaintext file contents are added to UI logs.

## Automation contract

`UpdatePreparationCancel` identifies the actual cancellation button.
`LegacyApprovalRequestId` identifies the query input.
`LegacyApprovalQuery` identifies the query button.
The source contract checks IDs and service handlers.
The existing GuiProbe and independent-profile procedures remain available.
Source contract PASS and tool build PASS are preparation evidence only.
Actual clicks, DPI, keyboard, screen reader, reduced motion and layout interaction remain NOT TESTED.
No screen-control session was started or reconnected during WO22.

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.
