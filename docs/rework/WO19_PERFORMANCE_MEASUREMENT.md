# WO-19 — Performance Measurement

WORK ITEM: WO-19
STATUS: PARTIAL
BASELINE SHA: 58e5e9ec08b677425b89050c6a85c076c3d87ff8
VALIDATION SOURCE: 347a34fa1f273db7064f40e3c53ecea4fd10ecee
SOURCE TREE: 8eedac7639287294ac158f52ea8f57f97b046aa7
BRANCH: codex/pccare-wo19-update-recovery-closure
RELEASE: Phase 1 NO-GO

INPUT: two 64 MiB synthetic payload files and one 3-byte original.
Logical payload size: 134217728 bytes.
Compressed package size: 131438 bytes.
Apply elapsed time: 5.288177 seconds.
Apply CPU time: 4.562500 seconds.
Maximum sampled working set: 53698560 bytes.
Sampling interval: 20 milliseconds.
Result: Committed:COMMITTED; exit 0.
Post-apply payload SHA-256 verification: PASS.

This workload repeats byte 0x57 and compresses strongly.
It is not a random binary workload or a throughput target.
The original backup is 3 bytes. This is not a large-backup benchmark.
No before-and-after product speed improvement is claimed.
The reference host has 24 logical processors and about 31.7 GiB memory.
Low-spec hardware and full GUI resource usage are NOT TESTED.

Evidence: `docs/rework/evidence/wo19/`

FINAL WINDOWS CI: PASS. Run 38576304293 verified source 347a34fa1f273db7064f40e3c53ecea4fd10ecee.
Full application: 1230 PASS / 111 SKIP / 0 FAIL. SecurityLab: 36 PASS / 0 FAIL.
WO-19: 16 PASS. Existing updater: 128 PASS. Performance: 233 PASS. These subsets overlap the full application total.
Process matrix: 20 apply and 13 recovery terminations. Deletion: 17 terminations. Quarantine: 6 terminations.
Restore and Release: 0 warnings / 0 errors. The dedicated VM and normal product GUI remain NOT TESTED.
CI evidence: CI_FINAL_RESULT.json, CI_JOB_LOG.txt, and CI_*.trx.

Termination accounting: 46 worker kills include 13 setup kills for the 13 recovery interruption points.
The matrix has 20 apply interruption locations and 13 recovery interruption locations.
Two image/DLL lock attempts returned expected exit 5. After lock release, recovery and reapplication passed.
