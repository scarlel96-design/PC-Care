# WO20 — UPDATE CRASH MATRIX

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Separate-process tests

The new harness tests one normal update interruption and eight legacy restore checkpoints.
It starts workers with hidden windows and kills the separate worker after its checkpoint signal.
Before promotion, no installation mutation is authorized and a reviewed retry is required.
After promotion, the existing schema 2 recovery path restores the approved original bytes.
Every restored original file is compared with its recorded SHA-256.

The eight legacy checkpoints are AuthorizationVerified, BeforePromotion, AfterPromotion and BeforeRestore.
They also include AfterFileRestore, SecondFileRestore, BeforeRestoredJournal and AfterRestoredJournal.
These checkpoints occur around complete file restores.
They do not prove recovery from every possible partial-byte interruption.

The existing WO06 harness retains twenty apply interruptions and thirteen recovery interruption scenarios.
Its process records and prior fault boundaries remain separate evidence.
An exception-based unit test is not counted as a killed worker.

## Missing physical evidence

Signed Bridge first transition, UAC, system restart and VM power-off are NOT TESTED.
File Flush and File.Replace do not establish physical storage durability.
Preserve original files and authenticated records when recovery cannot classify the current state.

## Evidence and reproduction

Read `evidence/wo20/VERIFICATION.txt` for commands, inputs, exits and source provenance.
Read the named JSON evidence for literal process outputs and SHA-256 comparisons.
Use only synthetic fixtures. Keep private fixture passwords and DPAPI files outside published evidence.

## Restore procedure

Use `ROLLBACK.sh` only on an independent source copy named RESTORE_COPY.
Use the trusted updater recovery entry point for authenticated schema 2 transactions.
Review schema 1 plans with the signed original-version package and explicit consent.
Preserve files when identity, content, ACL, journal or package checks fail.
Source rollback does not restore installation data or encrypted vault contents.
