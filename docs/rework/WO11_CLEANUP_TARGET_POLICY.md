# WO-11 — Cleanup target policy

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Classification

| State | Rule | Execution |
|---|---|---|
| SafeCandidate | Current owner; age at least seven days; .tmp or .cache; no protected attributes | Disabled |
| RequiresReview | Recent, future-dated, read-only, or unrecognized purpose | Disabled |
| InUse | Numeric Win32 sharing or lock violation | Disabled |
| Protected | Directory, system or reparse attribute, vault, binary, or backup extension | Disabled |
| UnknownOwnership | Owner mismatch or ownership access failure | Disabled |
| Unsupported | Unsupported file boundary or other access failure | Disabled |
| ScanIncomplete | Contract state for incomplete collection | Disabled |

Aggregate Complete=false reports incomplete scans. No fabricated size is assigned.
Protected extensions include .av3, .av4, .av5, .vault, .exe, .dll, and .bak.
No file becomes executable because its length is large.
SafeCandidate means review eligibility. It is not deletion approval.

## Preflight and consent

Dry Run validates the directory ID, file ID, owner, length, timestamp, and current attributes.
Duplicate candidate selection is blocked.
Changed candidates contribute zero preview bytes and require another scan.
Dry Run reads metadata. It does not prove full content integrity.
The synthetic executor verifies a known payload SHA-256 immediately before mutation.
Consent binds action and directory identity, with a two-minute expiry.
Final deletion uses a separate consent bound to the action and file identity.
The production application does not reference the executor.

## Legacy protection

The quality gate clears CanAutoApply for path-based cleanup findings.
The service also blocks Junk, Privacy, and Shortcut execution paths.
The second gate rejects forged findings and includeReview requests.
Existing legacy file cleanup requires a future reviewed execution contract.
