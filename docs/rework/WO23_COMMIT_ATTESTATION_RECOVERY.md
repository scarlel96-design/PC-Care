# WO23 — Commit and installation publication recovery

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Root cause
WO22 wrote the Committed journal before the signed installation declaration and current pointer.
A process could stop after journal commit while installation publication remained incomplete.
A journal stage alone did not prove installed-file authentication.

## New order
1. Verify every applied file through retained handles.
2. Write the signed declaration to a new transaction file.
3. Flush the declaration.
4. Write and flush the new installation pointer.
5. Publish the pointer with the existing File.Move or File.Replace operation.
6. Write the final Committed journal.
7. Close the retained writer handles.
8. Reopen and verify the installation declaration, signature, identities, ACLs and hashes.
9. Return success after verification.

The existing schema remains unchanged.
The final read requires closed writer handles because Windows file sharing checks are bilateral.
Verification failure still enters the existing rollback path.

## Interrupted publication
A published pointer with a non-Committed journal cannot authenticate an installation.
Recovery uses the existing transaction and verified backups.
The two-generation matrix also tests current-pointer replacement and previous-pointer preservation.
A corrupt current pointer does not automatically select the previous pointer.
The previous pointer is considered only under the existing authenticated restoration rules.

## Existing committed successors
An actual baseline worker stopped after its second Committed journal and before the new declaration.
The second package changed a different file from the first package.
The old reader incorrectly verified the unchanged first package through its obsolete pointer.
The modified reader returns INSTALLATION-RECOVERY-REQUIRED and preserves the entire fixture.
Apply also rejects this state before opening the next package or creating a transaction.

The reader checks other committed transactions against the signed declaration's normalized version.
This check can deny trust; it cannot select or authenticate a replacement generation.
The journal alone never grants update or recovery authority.
Three regressions cover obsolete pointers, missing successor declarations, normalized versions and normal successive updates.
Read `LEGACY_PUBLICATION_GAP_RESULT.json` for the separate-process reproduction.

## Limits
Several durable files do not form one atomic filesystem operation.
Flush(true) does not prove physical power-loss durability.
Old WO22 incomplete records remain review cases; this change does not invent missing signed evidence.
Read `COMMIT_CRASH_RESULT.json` and `INSTALLATION_POINTER_RESULT.json` under `evidence/wo23/`.
