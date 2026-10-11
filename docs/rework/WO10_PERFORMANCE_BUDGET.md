# WO-10 performance budget

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Local observation

Environment: Windows 10.0.26200.0; .NET SDK 10.0.401; x64 Release; 24 logical processors; normal application token.
The thirty-second normal-token run overlapped regression tests.
CPU sample count: 29.
Whole-system CPU median: 46.79196370706416 percent.
Whole-system CPU P95: 59.1877709633076 percent.
Collector whole-machine CPU cost: 0.08021476396278072 percent.
Collector CPU time: 0.578125 seconds.
Peak working set: 44331008 bytes.
Managed allocated bytes: 775448.
Analysis time: 51.2934 milliseconds.
These figures are one observation, not an improvement claim.
The parallel PDH companion task is not fully included in the collector CPU timer.

## Missing measurements

All local PDH CPU samples were null. Status is Unknown, with NoValidPdhCpuSamples.
ETW access was denied for the normal token. Remaining-session enumeration was unavailable.
The existing authorized fallback returned CPU and memory observations.
Live UI CPU, UI latency, startup time, and screen-reader cost remain NOT TESTED.
Low-end physical hardware remains NOT TESTED.
Processor-group correctness on high-core hardware remains NOT TESTED.
ReferenceProbe resource cost is a test-tool cost, separate from the resident collector.
Read PERFORMANCE_BUDGET_RESULT.txt and CI measurement summaries.

## Windows CI measurements

Run: 38113296563. Command: `./scripts/wo10-reference-validation.ps1 -OutputDirectory artifacts/wo10-ci -IsolatedRunner`.
Input: twelve thirty-second bounded synthetic workloads with concurrent WPR recording.
Exit code: 0.
Collector whole-machine CPU range: 0.467977 to 2.390558 percent.
Collector peak working-set range: 54124544 to 60473344 bytes.
Offline reference-analyzer peak range: 129593344 to 171651072 bytes.
Owned sessions remaining after each trial: 0.
Collector native-loss counters before stop, application drops, malformed events, and pending events were zero.
Collector terminal native-loss observation remains Unknown.
WPR metadata separately records zero native loss over each recorded reference trace.
The CPU timer does not fully include the companion PDH task.
Live UI cost, low-end physical hardware, and processor groups above 64 remain NOT TESTED.
Read CI_PERFORMANCE_BUDGET.json and individual measurement files for exact inputs and outputs.
