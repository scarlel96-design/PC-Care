# WO-11 — Recovery transaction

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Synthetic transaction

1. Create a generated pccare-wo10 directory in a pinned test parent.
2. Apply a protected ACL for the current user and SYSTEM.
3. Create the known fixture with CreateNew.
4. Record root ID, file ID, length, and SHA-256.
5. Obtain explicit fixture consent.
6. Acquire the exclusive operation lock.
7. Revalidate the exact target and consent.
8. Persist Prepared before rename.
9. Rename the opened file to fixture.quarantine.
10. Verify its identity and bytes.
11. Persist Quarantined.
12. Report measured storage fields separately.

The fixture size is bounded from 4 KiB through 32 MiB.
Schema 1 retains the original 4 KiB fixture contract.
Schema 2 permits bounded known-pattern fixtures.
No arbitrary user content is accepted.

## Recovery

Recovery accepts Prepared, Quarantined, RollingBack, or Restored.
Exactly one original or quarantine path must exist.
Identity and SHA-256 must match the journal.
Recovery records RollingBack before rename and Restored after verification.
A conflicting destination is preserved. Uncertain files are not overwritten.
The exclusive operation lock prevents concurrent execution and recovery.

## Final deletion

Final deletion requires Quarantined and a separate current consent.
DeletePrepared is persisted before opened-file deletion.
FinalDeleted is recorded after both payload paths are absent.
DeletePrepared and FinalDeleted reject automatic recovery and preserve evidence.
There is no rollback after final deletion.
This is ordinary fixture deletion. It is not secure deletion.

## Durability limits

The journal uses a bounded authenticated payload, flushed temporary file, and File.Replace.
Same-user key access and replacement races remain.
Forced process termination tests do not establish power-loss durability.
No installed product transaction or VM reboot was tested in WO-11.
