# WO-13 journal hardening

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Implemented checks

Journal append now propagates filesystem and validation failures.
The writer uses a static in-process gate and an exclusive cross-process writer lease.
Records require a 16-character hexadecimal transaction ID and a known state.
Transitions require Prepared and reject duplicate terminal states.
The reader rejects malformed JSON, invalid states, and invalid transitions.
The journal budget is 32 MiB. The line budget is 4096 characters.
Append flushes through the pinned file handle.
Invalid journal data forces authenticated access into read-only recovery.
Journal records cannot authorize encrypted object deletion.
Reconciliation only marks known authenticated object references.

## Evidence and residual risk

The journal writer lock failure propagated to the caller. The seed still exported with matching bytes.
Three corrupt-record cases blocked writes and preserved readable seed data.
Duplicate and invalid transition tests rejected their inputs.
Forged journal input preserved encrypted object bytes.
Real disk-full, ACL-denial, partial-write termination, and hostile journal path replacement remain NOT TESTED.
ReadValidated still uses path-based File.ReadLines. It is not an authenticated journal parser.
Append rereads prior records. Long-term append cost and journal rotation remain unqualified.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
