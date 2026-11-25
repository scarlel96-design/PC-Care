# WO-29 execution report

PARTIAL. The Windows probes exercised interruption inside recovery inspection, staged publication and authenticated v5 extraction. Source and qualification records are in evidence/wo29. See GITHUB_RESULT.txt for the tested revisions.

Guest deployment, UAC, administrator restore, reboot, GUI and physical power loss remain open. No main merge or release was performed.
## Verified results

| Windows qualification | Result |
|---|---|
| [App and engine regression](https://github.com/scarlel96-design/PC-Care/actions/runs/45535219952) | App: 1,445 passed, 0 failed, 111 skipped. SecurityLab: 36 passed separately. Release: 0 warnings, 0 errors. |
| Recovery inspection | The parent terminated 26 readers. Fresh readers found no unrequested changes. No inventory entries were excluded. |
| Verified resume | The original fixture needed no copy. The updated fixture was restored. Both unknown fixtures retained their current bytes for review. |
| [Staging and vault probes](https://github.com/scarlel96-design/PC-Care/actions/runs/45535220086) | 22 staging interruptions. 33 v5 opens and 132 authenticated file comparisons passed in process tests. |
| History experiment | At 4,096 entries, median cumulative allocations fell 36.64%. The identity check count remained 1,003,935. Timing ranges overlapped. |

The Windows workflows tested commit `e638ca51ca0e12082df0d00dffb037a583e097c0`. The history experiment used the separately identified build in [the comparison report](WO29_HISTORY_ALLOCATION.md).
See the [verification record](evidence/wo29/VERIFICATION.txt) for the independent source rollback. See the [readiness report](WO29_PRODUCT_READINESS.md) for open release requirements.
