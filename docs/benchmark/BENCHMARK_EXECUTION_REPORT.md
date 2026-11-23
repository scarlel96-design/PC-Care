# Benchmark Execution Report

**WO-28 PARTIAL. Phase 1 NO-GO.** REA 6.7.0 analyzed two managed fixtures and one JavaScript application. The active Codex session returned managed analysis evidence. The native provider was unavailable.

The [catalog](COMPETITOR_CATALOG.md) covers 63 distinct candidates. [Gap analysis](PC_CARE_GAP_ANALYSIS.md) and the [backlog](IMPROVEMENT_BACKLOG.md) connect five priority products to independently designed improvements. Research uses official documentation and selected public source. No competitor runtime, speed, deletion efficacy or accessibility score was measured. No third-party implementation was imported.

The read-only recovery inventory survived eight parent-triggered process stops across four cases. The marker became visible only after its contents were flushed and closed. Inventory did not run atomically across files. The engine had already returned at each stop. Engine-internal interruption remains open. The historical 4,096-entry measurements do not establish a new speedup. Cross-volume A/B remains experimental. Production keeps the same-volume requirement.

Priorities: qualify signed deployment and UAC on a disposable Windows VM; verify authenticated vault extraction after reboot; profile history allocations on identical inputs; qualify native REA separately. [Evidence directory](../rework/evidence/wo28/) contains commands, local and CI scope, and independent copy rollback.

## What the recovery tests establish

| Synthetic target | Bytes | Resume result |
|---|---:|---|
| OriginalVerified | 4,096 | ROLLED-BACK |
| UpdatedVerified | 1,048,576 | ROLLED-BACK |
| UnknownContent | 16,777,216 | RECOVERY-CONTENT-REVIEW |
| UnknownContent | 67,108,864 | RECOVERY-CONTENT-REVIEW |

Each case survived two interruptions during read-only inventory. A separate reader compared file hashes. UnknownContent required review and retained its existing bytes.
[Four-case evidence](../rework/evidence/wo28/RELIABILITY_RESULT.json).

The WO-27 4,096-entry runs used a synthetic Windows profile with strict ACL mode disabled. Median process allocations rose from 1,982,848,768 to 2,141,241,968 bytes.
Native identity checks rose from 442,582 to 553,180 per run. Measure those calls first. Preserve file identity, signature and ACL checks.
These six records predate WO-28. They do not measure a WO-28 speed change. [Allocation records](../rework/evidence/wo28/HISTORY_ALLOCATION_REANALYSIS.json).

## Windows qualification

[Run 45111151437](https://github.com/scarlel96-design/PC-Care/actions/runs/45111151437) passed at source `2aa9ac05a5d286ee3aa250274d0a8b0d07c2c62b`.
The app passed 1,445 tests, failed none and skipped 111. SecurityLab passed 36 tests with no failures or skips.
Restore emitted no warning or error diagnostics. Release reported zero warnings and zero errors.
The first run failed during compilation. Pinning SDK 10.0.402 restored comparability with WO-27.

The Windows midcopy script reported 47 cases. The separate restart inventory reported four cases.
The 51 interruptions in the WO-27 baseline are historical. No category totals from that baseline are attributed to this run.
The probe and sample results are separate from the app test count. See [test output](../rework/evidence/wo28/REGRESSION_RESULT.txt).
