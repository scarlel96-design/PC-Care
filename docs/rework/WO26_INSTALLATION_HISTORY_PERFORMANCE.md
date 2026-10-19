# WO26 — Installation History Performance

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Resource model
The history verifier retains authenticated declarations and read leases through final validation.
The former per-lease 128 KiB metadata buffer accumulated across retained declarations.
`OpenMetadata` uses a 4 KiB stream buffer with the same access, share, identity and parent-pin checks.
Ordinary file-copy and vault paths keep their existing buffers.
Path dictionaries remove repeated linear journal-file lookup.
No signature cache or historical trust cache is added.

## Measurement
The reused probe constructs signed 0, 1, 10, 100, 500, 1000 and 4096 transaction histories.
These are constructed metadata fixtures, not thousands of executed update transactions.
Each profile child has a 120-second and 512 MiB working-set stop budget.
A budget stop is reported separately and is not a verification PASS.
Profile-row PASS means the measurement process completed.
Read History.Verified separately to determine installation-history verification.
The sampler observes handle count and working set every 5 ms.
Short peaks can be missed.
Allocation and CPU measurements include the sampler and process/runtime overhead.
Fresh child-process runtime initialization is not subtracted.
Nested phase durations overlap and must not be summed.

An isolated comparison changes only InstallationHistoryQualification.cs between baseline and modified engines.
Six runs use the same 100-transaction fixture in interleaved baseline/modified order.
The comparison is component evidence, not a full deployment benchmark.
Record actual medians after both result files are available.

## Remaining cost
Read leases and directory ancestors still increase handle count with history size.
Large-history startup time remains product work even when memory stays below the experiment budget.
No low-end hardware qualification is claimed.

## Interleaved local component observations

| Metric | Baseline median | Modified median |
|---|---:|---:|
| ElapsedMs | 5610.4089 | 6540.9802 |
| CpuMs | 8937.5 | 10625 |
| AllocatedBytes | 83001816 | 71920040 |
| PeakSampledWorkingSetBytes | 73269248 | 56320000 |
| PeakSampledHandles | 1539 | 1536 |
| HandlesAfter | 296 | 293 |

Each condition has three fresh-process samples.
Concurrent regression and a broad timing spread prevent a controlled speed claim.
Sampled working set and cumulative managed allocation decreased in these observations.
The experiment does not prove full product startup improvement.

## Timing boundary

The timer measures InspectInstallationHistory, not the full WinUI application start.
The zero-transaction fixture contains synthetic files and represents unattested history.
Fresh empty-install startup latency and damaged-state startup latency are not newly benchmarked.
Their classification regressions are separate tests.
Maximum retained file leases remain linear in history size.

## Final scale observations

| Origin | Transactions | Status | Elapsed ms | Allocated bytes | Peak sampled WS bytes | Peak sampled handles |
|---|---:|---|---:|---:|---:|---:|
| CI | 0 | PASS | 29.8629 | 18376 | 31764480 | 253 |
| CI | 1 | PASS | 130.2687 | 1938472 | 42672128 | 307 |
| CI | 1 | PASS | 128.4316 | 1898752 | 42688512 | 307 |
| CI | 1 | PASS | 131.1298 | 1927384 | 42999808 | 311 |
| CI | 10 | PASS | 164.3826 | 6214944 | 46862336 | 361 |
| CI | 100 | PASS | 528.7876 | 49967064 | 48431104 | 1004 |
| CI | 100 | PASS | 522.5428 | 50085536 | 49192960 | 1004 |
| CI | 100 | PASS | 536.8778 | 50125528 | 49430528 | 1004 |
| CI | 500 | PASS | 2243.989 | 247266320 | 64311296 | 3807 |
| CI | 1000 | PASS | 4317.9089 | 493212616 | 67371008 | 7307 |
| CI | 4096 | PASS | 15755.3633 | 1982582656 | 135692288 | 28982 |
| Local | 0 | PASS | 32.3346 | 15488 | 35315712 | 281 |
| Local | 1 | PASS | 160.4 | 2501752 | 45961216 | 363 |
| Local | 1 | PASS | 178.4362 | 2519168 | 45842432 | 360 |
| Local | 1 | PASS | 174.5324 | 2516856 | 45895680 | 358 |
| Local | 10 | PASS | 274.4037 | 8625808 | 51798016 | 471 |
| Local | 100 | PASS | 1542.0043 | 71401296 | 55361536 | 1552 |
| Local | 100 | PASS | 1734.9162 | 71391504 | 55566336 | 1551 |
| Local | 100 | PASS | 1425.6498 | 71324536 | 55046144 | 1551 |
| Local | 500 | PASS | 6651.1146 | 346584096 | 76791808 | 6356 |
| Local | 1000 | PASS | 13710.1913 | 689665896 | 88338432 | 12358 |
| Local | 4096 | PASS | 52190.0222 | 2809713952 | 160100352 | 49508 |

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
