# WO23 — Approval reconciliation

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Existing authority model
Reuse LegacyApprovalLifecycle and the protected one-time approval receipt.
A RequestId reservation prevents reuse but does not prove restoration.
InspectLegacyApproval remains read-only.
It cross-checks the request, package, directory identities, schema promotion, backups and current file bytes.
CallerAcknowledged remains null.
A durable result cannot prove that the caller received a response.

## Additional regression
A partial reservation remains preserved and requires review.
The same RequestId cannot start another restoration.
Existing twelve checkpoint process tests retain reservation, promotion and restoration observations.
Completed restoration requires a terminal journal and all original file hashes.
UnknownContent remains preserved.
A backup alone does not authorize an unknown current-file overwrite.

## Limits
Synthetic tests use ephemeral package signatures and strictAcl=false.
They do not prove an elevated administrator session or interactive consent.
Actual administrator restoration remains OPEN.
Read `evidence/wo23/APPROVAL_RECONCILIATION_RESULT.json`.
