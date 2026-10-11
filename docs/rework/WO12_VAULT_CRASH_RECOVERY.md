# WO-12 vault crash recovery

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

## Actual process test

VaultProbe creates a fresh synthetic v5 vault for each stage.
A child unlocks the vault and imports 4,096 bytes.
The parent waits for the stage signal and terminates the child.
A separate reader process attempts unlock and byte-verified export.

| Termination stage | Reader result | Ciphertext preservation | Verdict |
|---|---|---|---|
| ObjectsReady | Exit 0; VERIFIED_ENTRIES=1;WRITE_ALLOWED=False | Existing object prefixes remain unchanged | PASS for old-data recovery |
| MetadataReady | Exit 4; PRESERVED_FAIL_CLOSED | Existing ciphertext remains; unlock blocked | PARTIAL |
| CommitWritten | Exit 0; VERIFIED_ENTRIES=2;WRITE_ALLOWED=True | Existing bytes remain; both entries export | PASS for tested import |

The tool exits 0 because the expected preservation outcomes occurred.
Its exit code does not convert MetadataReady into successful recovery.
The tool uses ImportBytes. Large streaming import interruption remains NOT TESTED.
Password-change interruption, rollback interruption and physical power interruption remain NOT TESTED.

## Recovery procedure

Preserve the complete damaged fixture before investigation.
Use a separate copy for forensic analysis.
Retain objects, encrypted metadata, headers, activation markers and journal records.
Do not rewrite the commit marker merely to bypass the mismatch.
Implement authenticated metadata generations before enabling automatic recovery for this case.
Verify exported original bytes in a new process after that implementation.

The source-copy ROLLBACK.sh restores one source file in an independent copy.
It does not recover an installed application or encrypted vault.
