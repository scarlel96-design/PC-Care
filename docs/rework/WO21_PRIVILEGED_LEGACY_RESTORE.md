# WO21 — PRIVILEGED LEGACY RESTORE

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Approval contract

LegacyRestoreRequest now carries RequestId, RequestedAt and explicit user-consent intent.
The UI pins the request file and passes its full hash to the Bridge.
The Bridge pins the file again and compares that exact hash.
The engine rejects duplicate JSON properties and unknown top-level members.
It checks the ten-minute intent lifetime and recomputes the signed recovery plan.
It compares all targets before asking for confirmation.
The trusted elevated console displays the reviewed transaction and target list.
The operator must type the complete RESTORE transaction phrase.
Redirected input or output rejects interactive approval.
UAC approval alone is not treated as approval of the target list.

A protected CreateNew approval record binds the request file ID, hash, plan, package, targets, time and administrator session.
The engine flushes this record before schema promotion or target mutation.
The same RequestId cannot acquire another record after interruption or failure.
The record is a consumed approval reservation. It does not prove successful restoration or response delivery.
A reviewed retry needs a new request and a new explicit approval.
The installation journal remains the restoration state source.

The older boolean-only ExecuteLegacyRecovery entry rejects strict production execution.
The approved entry reuses that method and RollbackCore after the protected approval step.
The synthetic test path uses strictAcl=false and an injected confirmation callback.
It proves file behavior, not administrator identity or real operator consent.

## Preserved evidence

The original schema 1 journal remains under its SHA-256-derived retained name.
Backups remain intact.
Package identity, journal hash, directory IDs, target ACLs, lengths and full hashes are checked again.
Unknown current content remains blocked, even when an authenticated old backup exists.
Multiple pending transactions remain blocked.
Actual administrator deployment remains NOT TESTED. P0-2 remains OPEN.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
