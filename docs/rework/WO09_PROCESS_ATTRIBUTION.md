# WO-09 process attribution

Status: PARTIAL.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
Validated source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Implementation

ProcessIdentityRegistry remains the shared lifetime contract for PC-Care ETW and polling.
I/O candidates use resolved session lifetime identifiers.
ProcessSelection reserves up to one third of the 128-entry budget for observed I/O leaders.
CPU and working-set leaders fill the remaining budget.
Unresolved I/O identifiers do not join by PID alone.
Access failures, exits, metadata omissions, and unlinked events remain visible limitations.

The 200-process synthetic test includes a low-CPU I/O leader behind the previous selection limit.
The leader is selected without increasing the detail budget.
Negative I/O and unresolved identities are rejected in separate tests.
Existing WO-08 PID reuse tests remain in the regression suite.
Actual Windows PID reuse is NOT TESTED unless a recorded trial demonstrates it.

ReferenceProbe exports independent anonymized process lifetimes, CPU weights, and issuing-process I/O.
The reference salt differs from the PC-Care session salt.
There is no private cross-parser identity join yet.
Real process contribution error and cross-parser lifetime accuracy remain NOT TESTED.
These two anonymous outputs must not be compared by identifier equality.
CPU sample weights describe observed activity. They do not establish application root cause.


## Evidence and restoration

Read `evidence/wo09/VERIFICATION.txt` for commands, inputs, outputs, and source-copy restoration.
Read `evidence/wo09/GITHUB_RESULT.txt` for the publication commit and CI source.
Use a reviewed Git revert to restore repository changes.
The source-copy rollback does not test an installation transaction.

## Final independent observations

Six trials exported 6, 6, 7, 7, 8, and 9 independent lifetimes.
Read CI_REFERENCE_1.json through CI_REFERENCE_6.json for exact lifetime and unlinked-event records.
Independent process outputs remain separate from PC-Care lifetime identifiers.
No common private join establishes cross-parser process identity accuracy.
Contribution error remains null. Actual Windows PID reuse remains NOT TESTED.
