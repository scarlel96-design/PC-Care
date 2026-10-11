# WO-09 independent reference pipeline

Status: PARTIAL.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
Validated source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Capture and interpretation

The WO-08 WPR trace reported 39,336 lost events. It is excluded from ground truth.
The new WPR profile uses file mode, 64-KiB buffers, and 128 buffers.
The configured system buffer allocation is 8 MiB.
The profile enables process/thread, loader, context-switch, sampled CPU, DiskIO, and DiskIOInit events.
It omits stack capture.
Loader and context-switch events support process resolution and scheduling diagnostics.
Each trial records workload, collector, and trace times.
The comparison window intersects all three timelines and excludes the first two workload seconds.
Complete sample intervals are retained. Boundary-crossing intervals are excluded.

ReferenceProbe uses typed TraceProcessing metadata, CPU, process, and disk APIs.
The parser accepts initialized converted timestamps, including rounded partial SDK timestamps.
IsPartial restricts raw TraceTimestamp.Value access. It does not invalidate DateTimeOffset.
Converted times must remain inside the trace window.
Converted weights must remain finite, positive, and at most one second.
Conversion errors exclude the input.
The SDK contract fixture exercises ten conversion and rejection cases.

CPU sample weights divide by elapsed seconds and all logical processors.
Unresolved processes and invalid weights exclude affected CPU bins.
Loss counters, lost buffers, and CPU gaps must equal zero.
Unknown loss counters do not qualify a reference.
Independent I/O output includes bytes, operations, latency, and trace-local disk numbers.
Memory and disk pressure ground truth remain unqualified.
The SDK exports the scheduling time-slice count as a parser diagnostic.
Scheduling data does not supply qualified pressure truth in this implementation.
SDK sampling gaps represent estimated, unaccounted periods between samples.
They are distinct from native ETW lost-event counters.
The original zero-gap qualification requirement remains unchanged.
Sample weights estimate CPU time. They do not provide exact CPU time.

## Trace handling

The harness requires explicit IsolatedRunner and GitHub Actions context.
Private directories exclude inherited access and permit the runner, SYSTEM, and administrators.
The report records ACL protection and child inheritance.
An existing WPR session causes failure before capture.
The harness only cancels its own session.
It preserves ETL until independent analysis succeeds.
Successful analysis records SHA-256 and deletes only the private ETL.
Failed analysis retains ETL for the remaining runner job lifetime.
Raw ETL, process names, command lines, storage serials, and identity keys are excluded from uploads.
This bounded ephemeral retention does not provide long-term independent ETL reanalysis after the job.


## Evidence and restoration

Read `evidence/wo09/VERIFICATION.txt` for commands, inputs, outputs, and source-copy restoration.
Read `evidence/wo09/GITHUB_RESULT.txt` for the publication commit and CI source.
Use a reviewed Git revert to restore repository changes.
The source-copy rollback does not test an installation transaction.

## Final Windows CI observations

CI [38105566632](https://github.com/scarlel96-design/PC-Care/actions/runs/38105566632) completed successfully on source `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
The source tree is `bfa20d37d8adca1d365d96efbd40983aff988678`.
The runner reported Windows NT 10.0.26100, four logical processors, and an elevated token.
All six trials used a 28-second common comparison window.

| Trial | Workload | WPR lost events | WPR lost buffers | SDK CPU gaps | Valid aligned CPU samples | Independent lifetimes | Collector machine CPU % | Collector peak MiB |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | cpu | 0 | 0 | 121988 | 111988 | 6 | 0.675593 | 56.777 |
| 2 | cpu | 0 | 0 | 121816 | 111988 | 6 | 0.740397 | 52.855 |
| 3 | cpu | 0 | 0 | 122144 | 111988 | 7 | 0.701551 | 51.773 |
| 4 | peak | 0 | 0 | 121724 | 111988 | 7 | 0.649949 | 54.207 |
| 5 | peak | 0 | 0 | 121852 | 111988 | 8 | 0.766655 | 57.715 |
| 6 | peak | 0 | 0 | 121764 | 111988 | 9 | 0.818504 | 53.594 |

All six workload commands completed. All six SDK analysis commands returned exit code zero.
Zero trials passed the predeclared CPU reference qualification gate.
The SDK gap counts are estimated unaccounted sampling periods. They are separate from native ETW loss.
Precision and Recall remain null. Unknown is 18 across three resource categories and six trials.
True Positive, False Positive, False Negative, and True Negative are zero qualified classifications.
These zeros do not establish zero product errors.
Memory and disk pressure truth remain Unknown.
Process contribution error and disk measurement error remain unmeasured.

The final parser accepted converted timestamps and durations from partial SDK contexts.
Each trial supplied 111,988 valid CPU samples in the aligned window.
Invalid timestamp and invalid duration counts were zero in all six trials.
The SDK conversion fixture passed ten cases.
Some CPU bins slightly exceeded 100 percent because sampling weights estimate elapsed activity.
The parser preserves these observations. It does not clamp them into qualified truth.
The zero-gap gate already excludes these trials. The range check remains an additional qualification condition.

PC-Care reported zero native ETW loss and zero application queue drops in all six trials.
All six session cleanup results were VerifiedAbsent.
The native application loss reading occurs before stop. Its terminal interval remains Unknown.
The final trace analyzer used 495.642 to 684.972 milliseconds.
Its peak working set ranged from 121.504 to 145.180 MiB.
Analyzer CPU time was not measured.
The collector values describe one hosted runner. They do not qualify a hardware fleet.
The sequential CPU-then-peak trial order leaves cache and background-work confounding.
The next qualification must investigate the SDK sampling gaps and repeat counterbalanced trials.
