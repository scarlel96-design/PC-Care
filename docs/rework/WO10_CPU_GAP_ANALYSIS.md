# WO-10 cpu gap analysis

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Distinct counters

Metadata.LostEventCount records native trace event loss.
Metadata.LostBufferCount records native trace buffer loss.
ICpuSampleDataSource.Gaps describes estimated unaccounted sampling periods.
CpuGaps is not renamed to native ETW loss.
Native loss zero does not establish correct utilization semantics.
Sampling gaps do not prove that every missing interval was Idle.
The parser exports conversion failures, partial fields, unknown samples, and scheduling coverage separately.

## WO-09 reinterpretation

WO-09 recorded native loss zero in six trials.
Its roughly 120000 CpuGaps and near-100-percent estimates require interpretation beyond transport loss.
The original WO-09 JSON and reports remain unchanged.
The original ETLs were deleted after analysis. Exact event-level retrospective replay is therefore unavailable.
The previous sampled zero-gap acceptance gate remains unchanged.
WO-10 keeps legacyReference and legacyQualification beside the new scheduling observation.
This preserves both calculations in each new trial.

## Remaining diagnosis

Raw versus correlated switch-in behavior requires further SDK and WPA comparison.
Sampling frequency, Idle coverage, boundary truncation, DPC, and ISR require independent validation.
The evidence does not assign one definitive cause to every historical gap.
The new gate prevents unqualified scheduling observations from becoming accuracy truth.

## Fresh trials and preserved historical evidence

The original sampled calculation already excludes Process.Id zero.
WO-10 retains that calculation unchanged.
Fresh idle sampled medians were 1.7507 and 2.5138 percent.
Fresh sustained-load sampled estimates reached approximately 100 percent.
The retained historical peak results were already below 100 percent.
The claim that all six historical trials returned near 100 percent conflicts with the retained JSON.
The original ETLs are no longer available.
CPU_GAP_DIAGNOSIS.json preserves six historical JSON medians and twelve fresh observations.
The scheduling adapter is an additional observation path, not proof of a historical formula repair.
Native loss remains separate from SDK sampling gaps.


## Baseline narrative correction

WO-09 trials 1-3 used sustained CPU workloads and returned near-100-percent sampled medians.
Peak trials 4-6 returned medians of 1.72558, 28.35959375, and 1.8503375 percent.
The retained JSON contradicts the work-order premise that all six estimates were near 100 percent.
The original evidence remains unchanged.
WO-10 does not claim a proven universal 100-percent calculation defect or its repair.
