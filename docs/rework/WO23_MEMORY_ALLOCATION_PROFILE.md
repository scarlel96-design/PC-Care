# WO23 — Memory allocation profile

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Measurement method
Use the same 258-file package for baseline and modified source copies.
Run three measured trials per variant in alternating order.
Run one additional profiling-disabled control per variant.
Verify every resulting file SHA-256.
Keep all 779 journal writes and 779 journal Flush calls.
Record elapsed time, CPU time, total allocated bytes, peak Working Set and collection counts.
Record the last completed GC large-object heap size.
Do not present that heap value as a peak.

## Allocation attribution
Each synchronous phase records GetAllocatedBytesForCurrentThread.
The NativeIdentityCheck phase isolates Check calls from journal serialization.
Nested phase values overlap and cannot be added.
The baseline backport adds measurement scopes only.
It preserves the original 32,768-character allocation.

## Implementation
Start GetFinalPathNameByHandleW with 512 characters.
Retry with the reported size when the result exceeds that buffer.
Retain the 32,768-character bound and full normalized-path comparison.
Retain file ID, type, link-count and parent-handle validation.
A regression covers final paths longer than 512 characters.
[Windows buffer-size contract](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew).

## Interpretation
The measurements separate cumulative allocation from process Working Set.
Allocation and CPU counters cover Apply.
Peak Working Set covers the whole process, including startup and post-Apply verification.
One profiling-disabled trial does not establish a precise causal profiler overhead.
Read `evidence/wo23/MEMORY_PROFILE_RESULT.json` for actual counters and medians.
Remaining allocation and strict-ACL cost require further profiling.
No general PC speed improvement or low-end hardware claim follows from this benchmark.

## Final repeated measurements
The executable source is `2c1c56b5995f303d57d98098e6eafeefc8e1df11`.
Run three interleaved profiling-disabled trials per variant.
Run one additional profiled trial per variant.
All eight final trials committed and verified 258 file hashes.

| Metric | Baseline median | Modified median |
|---|---:|---:|
| ElapsedMs | 21945.1052 | 21191.6097 |
| CpuMs | 17578.125 | 17609.375 |
| AllocatedBytes | 20283329528 | 985376208 |
| PeakWorkingSetBytes | 208551936 | 212447232 |

The earlier profiling-disabled follow-up contained one replacement failure.
Its actual rollback restored all 130 originals and removed all 128 transaction-created files.
The failure remains recorded in PERFORMANCE_TRIAL_FAILURE.json.
Its five preceding timings were not persisted and are unavailable.
The initial timing control inverted the profiled timing direction.
These shared-host batches do not establish a general speed improvement.
Physical hardware and strict-ACL performance remain unqualified.
