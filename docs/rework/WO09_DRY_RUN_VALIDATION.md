# WO-09 dry-run validation

Status: PASS.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
Validated source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Actual invocation

A compiled fixture invokes the production RecoveryPlanner and DryRun methods.
It supplies Candidate, Unknown, ExpectedWorkload, and InsufficientEvidence synthetic findings.
The Candidate produces PreviewAvailable. The other three inputs produce blocked plans.
All four outputs retain ExecutionEnabled=false and SystemMutations=0.
The fixture exited zero.

The UI invokes the same DryRun method and displays its preconditions and mutation count.
It does not claim an actual recovery action succeeded.
Invalid action, absent consent, invented improvement, executing state, weak coverage, and unsupported evidence have regression tests.
The preview contains no executable target or mutation API.
No system-change rollback was needed because the test performs no system change.
Source-copy restoration is documented separately from installation recovery.
Read DRY_RUN_SAMPLE.json for the actual full contract output.


## Evidence and restoration

Read `evidence/wo09/VERIFICATION.txt` for commands, inputs, outputs, and source-copy restoration.
Read `evidence/wo09/GITHUB_RESULT.txt` for the publication commit and CI source.
Use a reviewed Git revert to restore repository changes.
The source-copy rollback does not test an installation transaction.
