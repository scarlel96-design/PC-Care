# WO-09 recovery planner

Status: PASS.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
Validated source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Contract and eligibility

RecoveryPlan contains every requested field from PlanId through ExecutionState.
It reuses Finding, BottleneckAnalyzer, and the existing SystemCare UI adapter.
Evidence references include FindingId, measurement window, and provider.
CPU saturation and disk I/O pressure propose application-load review.
Memory pressure proposes background-load review.
A disk I/O observation does not invent a disk-space shortage.
Eligibility requires at least eight samples, 80% evidence coverage, and 50% measurement quality.
Only Candidate and ConfirmedObservation can create ReviewOnly plans.
Unknown, insufficient, reference-mismatched, and expected workload states remain blocked.
A Candidate authorizes review only. It does not authorize a system change.

The eight action labels include startup, background, cache, space, components, updates, apps, and services.
Current resource evidence supports two review action types.
The other action types remain blocked until their specific evidence contracts exist.
No external rule source, shell command, service controller, file deletion, or registry writer exists in this planner.
ExpectedEffect and resource cost remain Unknown.
Execution risk requires target inspection before any later execution feature.
ExactTargetNotSelected is explicit in the preview.
User consent and state backup remain prerequisites for a future executor.
Rollback is not applicable to preview and is mandatory before execution.
Verification includes aligned CPU, memory pressure, disk latency, and task completion measurements.
ExecutionState remains PlannedOnly.


## Evidence and restoration

Read `evidence/wo09/VERIFICATION.txt` for commands, inputs, outputs, and source-copy restoration.
Read `evidence/wo09/GITHUB_RESULT.txt` for the publication commit and CI source.
Use a reviewed Git revert to restore repository changes.
The source-copy rollback does not test an installation transaction.

## Action-specific evaluation

| Candidate | Required evidence | Current decision | Execution risk and effect |
|---|---|---|---|
| Startup management | Boot delay and identified startup contribution | Blocked: missing startup contract | Disabling required software can break workflows. Effect Unknown. |
| Background review | Sustained memory pressure with complete observations | ReviewOnly when gates pass | Process termination remains unavailable. Effect Unknown. |
| User cache review | Cache ownership, disposability, and selected target | Blocked: missing cache contract | Incorrect ownership can cause data loss. Effect Unknown. |
| Disk-space review | Per-volume free space and selected cleanup targets | Blocked: missing space contract | I/O latency does not prove low space. Effect Unknown. |
| Windows component check | Read-only component health observations | Blocked: missing component contract | Repair privileges and rollback need separate evaluation. Effect Unknown. |
| Update-state check | Trusted update state and failure evidence | Blocked: missing update contract | The existing update engine remains authoritative. Effect Unknown. |
| Application-load review | Sustained CPU or disk pressure with complete observations | ReviewOnly when gates pass | Load can be expected. Review does not authorize termination. Effect Unknown. |
| Service configuration review | Service identity, dependency graph, and measured contribution | Blocked: missing service contract | Core-service changes can break Windows. Effect Unknown. |

No candidate carries an executable command.
A later executor must independently validate consent, target identity, preflight state, and rollback capability.
The current Dry Run cannot approve or perform that execution.
