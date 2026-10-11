# WO-10 rollback validation

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Actual fixture rollback

Six forced-termination scenarios returned the original 4096 bytes.
Their shared SHA256 is 0FCCC690DC9BE2CE29BA17504BDB68E81B8210D8893A29A7858639158E92298A.
Recovery is idempotent after restoration.
An original-path collision blocks recovery and preserves both files.
Changed quarantine data or journal authentication failures block mutation.
Read ROLLBACK_RESULT.txt for the literal output and LOCAL_SANDBOX.json for each checkpoint.

## Independent source-copy rollback

ROLLBACK.sh invokes SOURCE_ROLLBACK.ps1.
The script accepts only a directory named rollback-copy.
It checks the modified copy hash, restores ORIGINAL_FILE.cs, and checks the restored hash.
The fixture again returns InsufficientEvidence=0;Unknown=1;TrueNegative=0.
The working ReferenceComparison.cs and MODIFIED_FILE.cs retain the correction.
This check restores a source copy. It does not restore a product installation.

## Repository rollback procedure

1. Create a new branch from the published result.
2. Revert the implementation commit c23802b2adc7be63c8c15ab6cd313ec9be0a05db.
3. Run the full application and SecurityLab regressions.
4. Publish the reviewed revert without rewriting existing history.

Keep synthetic recovery separate from updater installation transactions.
No installed user application was modified during WO-10.

## Windows CI confirmation

Run 38113296563 repeated six separate-process forced-termination recoveries.
All six returned Restored with BytesVerified=true.
Hardlink, junction, and symbolic-link substitutions were rejected with exit code 3.
These results cover synthetic Windows filesystem operations.
Installed-product recovery, Windows reboot, and physical power loss remain separate qualification requirements.
