# WO-07 diagnostic architecture

Status: PARTIAL.

## Selected structure

The new Diagnostics project targets .NET 10 and Windows 10.0.26100.0.
TraceEvent provides ETW session management and event parsing.
CsWin32 generates PDH and system snapshot bindings.
PC-Care provides normalization, evidence rules, resource budgets, and UI adaptation.

| Component | Responsibility | Boundary |
|---|---|---|
| PerformanceCollector | Coordinate observation and cancellation | Read-only, 2–120 seconds |
| SamplingPolicy | Validate collection budgets | 1–5 seconds; 128 processes; 4096 events |
| CounterProbe | Read English PDH counters | Missing and invalid counters become null |
| SystemSnapshotProbe | Read CPU, physical memory, and commit | CPU requires two monotonic readings |
| EtwCollector | Parse kernel numeric events | Existing elevated context only |
| EventBuffer | Retain bounded event records | Nonblocking writes; explicit loss count |
| Normalizer | Reject invalid measurements | No replacement with healthy values |
| BottleneckAnalyzer | Classify sustained pressure | Candidate only; no repair execution |
| PerformanceFindingAdapter | Connect CareFindingQualityGate | CanAutoApply=false; no file target |
| PerformanceDiagnosticsPage | Display metrics and evidence | UI updates at polling frequency |

## Existing structure audit

CoreDashboardBridgeService measures app working set and free space. It does not measure persistent system bottlenecks.
CareSystemProbes uses point-in-time state and several text responses. Empty results can represent incomplete observation.
CareFindingQualityGate filters findings. It does not turn state checks into statistical measurement.
RootCauseScoringService uses rule weights, recurrence, and token overlap. Its score is not a probability of causation.
SystemCareService combines probes with repair tasks. Its behavior remains unchanged in WO-07.
The new page does not call repair tasks or scan cleanup paths.

## Observed data and gaps

Native probes provide total CPU, physical memory bytes, available memory, commit bytes, and commit limit.
PDH provides per-logical-CPU load, aggregate disk rates, operations, latency, queue, and memory page reads.
Process sampling provides CPU deltas, CPU time, and working set for at most 128 accessible processes.
The retained process subset follows enumeration order. It is not a guaranteed list of top processes.
ETW can provide per-disk events, profile samples, hard faults, context switches, and process I/O attribution.
ETW PID identifiers have no lifetime binding. They are not joined with process-lifetime identifiers.
Run-key value counts and uptime provide inventory context. They do not establish startup-delay causation.
Temperature, clock, thermal throttling, service correlation, and startup impact remain Unknown.
Systems above 64 logical processors receive an unknown native CPU aggregate.
Processor-group coverage requires a separate implementation and measurement.

## Future comparison contract

Each snapshot stores UTC start, monotonic offsets, environment context, errors, and resource costs.
Each finding stores its measurement window, sample count, provider, confidence, limitations, and next check.
Compare repeated captures only under comparable workloads and hardware.
Do not calculate a repair improvement rate from these unmodified-system observations.

## Validation boundary

Phase 1 release remains NO-GO. This branch does not authorize production deployment.
Windows 10, disposable VM, GUI interaction, and independent PerfView comparisons remain NOT TESTED.
Elevated kernel ETW execution remains BLOCKED on the available nonadmin host.
No production trust gate, updater, vault writer, deletion engine, or user settings changed.

The existing application declares Windows 11 minimum build 22000. Windows 10 product support is not established by this work.
