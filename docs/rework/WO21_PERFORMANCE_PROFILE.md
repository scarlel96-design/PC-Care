# WO21 — PERFORMANCE PROFILE

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Measurement method

The local comparison uses one randomly generated package copied before each trial.
Each trial updates 128 existing 4 KiB files, creates 128 new files and updates two small files.
Six matched trials ran in interleaved order.
Each variant used final source930 plus one explicit journal-formatting difference.
Both variants used identical profiling scopes and manifest indexing.
The WO20 53,839.12ms result used a different run and is not a direct comparison baseline.
Read PERFORMANCE_RESULT.json for literal samples, CPU time and peak working set.

Opt-in profiling measures package verification, extraction, backups, replacement and journal stages.
Boundary metrics measure hashing, identity validation, ACL reads and copy/flush time when active.
Nested measurements overlap. Do not sum them.
The synthetic strictAcl=false run does not measure protected ACL query cost.

The change writes compact JSON and indexes the already validated manifest by path.
It retains every journal write, Flush(true), identity check, hash check and rename step.
Each 258-file trial still executes 779 journal writes and 779 journal flushes.
Earlier exploratory trials had unequal instrumentation and are not used for the final comparison.
The small median difference does not establish a general speed improvement.
Low-end hardware, cancellation latency and physical-media durability remain NOT TESTED.

## Matched results

Indented median elapsed time:21015.7196ms.
Compact median elapsed time:20926.176ms.
Indented median CPU time:19328.125ms.
Compact median CPU time:19593.75ms.
Final journal size changed from173010 to137344bytes.
Peak working set samples ranged from186966016 to202207232bytes.
The elapsed difference is small. CPU and memory did not improve in this experiment.
No performance improvement is established.
The 779 durable journal operations remain the main measured cost.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
