# WO-10 recovery execution design

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Product and test boundaries

RecoveryPlanner continues to create PlannedOnly plans.
RecoveryDryRun continues to return ExecutionEnabled=false and SystemMutations=0.
The product assembly has no RecoverySandbox dependency or SandboxRecovery type.
No product UI command invokes the test executor.
The new standalone test tool reuses WindowsFileBoundary through UpdateEngine.
It does not inherit updater elevation or installation permissions.

| Responsibility | Implementation |
|---|---|
| ActionDescriptor | Fixed fixture and quarantine names; SyntheticOnly risk. |
| TargetIdentity | Root identity, file identity, length, SHA256. |
| PreflightValidator | Pinned directory and verified exclusive file lease. |
| ConsentRecord | Action and root binding; explicit approval; two-minute expiration. |
| ActionExecutor | Handle-based rename inside the fixture root. |
| ExecutionJournal | HMAC-authenticated bounded JSON; atomic replacement attempt. |
| RollbackHandler | Verified rename to the original path; no overwrite. |
| PostActionVerifier | Exact file identity, byte length, and SHA256. |
| RecoveryResult | State, BytesVerified, Mutations. |

Consent is checked again after preflight, immediately before the Prepared journal.
Cancellation before movement preserves the original.
Cancellation or failure after movement requires verified recovery.
The transaction lock prevents simultaneous apply and recovery.
The executor accepts only its fixed 4096-byte synthetic fixture.

## Remaining engineering limits

The journal key is local to the same owner-controlled fixture root.
This is not a defense against a hostile owner who can modify the key.
Journal replacement has residual same-owner races after the old journal handle closes.
Flush and File.Replace do not prove physical power-loss durability.
Creation-time orphan fixtures remain preserved for inspection.
These limits prevent production execution approval.

## Future actions

Prioritize reversible cache quarantine after target ownership, exclusion, retention, and measurement contracts exist.
Review startup and background resource evidence before proposing a change.
Keep Windows component and update checks read-only until their execution risks are reviewed.
Treat disk-space cleanup as ordinary reversible recovery, separate from secure deletion.
Require action-specific consent, pre-state, rollback, and before/after measurements.
No performance-improvement percentage is estimated in WO-10.
