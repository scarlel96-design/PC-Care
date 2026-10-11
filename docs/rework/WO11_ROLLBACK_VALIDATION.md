# WO-11 — Rollback validation

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Actual file transaction results

An 8 MiB fixture moved into quarantine and returned to its original path.
Original and restored SHA-256 matched:
`C9E31EA40540ABF97516878BA89B95B324B6B684299662B2B8561EDEB25AB5E2`.

Six separate processes were forcibly terminated at transaction checkpoints.
Each recovery used another process and returned exit 0 with matching original bytes.
Checkpoints: Prepared, Moved, Quarantined, RollingBack, RestoredMove, and Restored.
Hardlink, junction, and symbolic-link cases rejected execution with exit 3.
Two final-deletion interruption cases required review with exit 3.
Their journals remained unchanged. DeletePrepared retained its quarantine payload.
The Deleted checkpoint had no payload to restore.

Unit tests also cover changed IDs, changed bytes, file locks, read-only changes, duplicate selection, and expired consent.
Disk-full injection, different-user ACL denial, actual reboot, and power interruption remain NOT TESTED.

## Source-copy rollback

ROLLBACK.sh calls ROLLBACK.ps1 on an independent source copy.
The script validates the modified hash and restores the preserved original bytes.
The fixture then reported CanAutoApply=True after a forced rebuild.
The production source remains modified and reports CanAutoApply=False.
This test establishes source-copy restoration only.
It is separate from the real filesystem recovery evidence above.

## Operator procedure

1. Preserve the uncertain fixture directory and journal.
2. Run RecoverySandbox recover only on its generated fixture root.
3. Verify the resulting SHA-256 against the preflight hash.
4. Retain both files when recovery reports conflict or uncertainty.
5. Retain final-deletion evidence for manual review.

For source review, create another worktree at the baseline commit.
Do not reset the current working branch to perform a comparison.
