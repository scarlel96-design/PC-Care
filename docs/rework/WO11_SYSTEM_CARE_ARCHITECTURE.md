# WO-11 — System care architecture

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Implemented flow

StorageInventory reads bounded metadata through WindowsFileBoundary.
StorageCareViewModel runs the scan off the UI thread.
SystemCareCenterPage displays drives, candidates, exclusions, and preview details.
The user can select multiple candidates and request Dry Run.
Dry Run reopens the file and validates identity, owner, length, timestamp, and attributes.
The preview reports zero mutations. Production execution remains disabled.

The test tool exercises RecoverySandbox separately.
Its target is fixed synthetic content in a generated directory.
No product candidate can transfer execution authority into that tool.

## Reused components

- WindowsFileBoundary: ancestor pinning, file identity, held handles, link rejection, rename, and opened-file deletion.
- RecoverySandbox: consent, exclusive transaction lock, signed journal, checkpoints, integrity verification, and recovery.
- DiagnosticLifecycle: cancellation, session leases, and stale-result rejection.
- ObservableObject: bounded view bindings.
- CareFindingQualityGate: prevent legacy path-based cleanup authority.
- SystemCareService: reject file-cleanup actions even when a caller supplies forged auto-apply metadata.

## Existing engine audit

Legacy folder findings describe discovered paths. They do not preserve an exact execution snapshot.
A finding's confidence score cannot prove file identity or ownership.
CareFolderScanner remains useful for legacy read-only reporting.
WO-11 does not treat that report as a new execution capability.
Registry and other existing non-file actions retain their original policies.
CPU reference qualification remains the WO-10 PARTIAL result.

## Changed shared boundary

WindowsFileBoundary now preserves Win32Exception as IOException.InnerException.
This permits numeric sharing-violation classification without parsing error messages.
Handle sharing, identity checks, permissions, and update trust policies are unchanged.
All 128 update regression cases passed after this change.

## Remaining boundaries

The read-only scanner covers the process Temp root, without recursion.
Additional cache roots require explicit allowlist and retention-policy review.
The synthetic journal key remains accessible to the same user identity.
The journal replacement path retains a same-user race window.
Neither property qualifies the executor for arbitrary production files.
