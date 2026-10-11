# WO-10 cpu reference semantics

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Confirmed SDK contracts

ICpuSample.Weight estimates the duration until the next sample.
It supports sampled attribution. It does not prove actual busy time by itself.
SampleCount counts observations. It is not a duration or an accuracy percentage.
Sampling frequency and missing observations affect the estimate.
The legacy formula sums weights and divides by logical processor count and observation time.
All sampled weight cannot be classified as busy time without verified Idle attribution.
Process.Id == 0 alone is not accepted as proof of complete system Idle coverage.
The report exports sampled Idle-thread and missing-thread counts separately.

## Scheduling adapter

ReferenceProbe uses UseCpuSchedulingData and the existing SDK CpuTimeSlices.
The adapter requires matching switch-in and switch-out thread IDs.
Thread ID zero is an Idle candidate. Missing boundaries produce Unknown time.
CpuSchedulingReference clips intervals into one-second bins for each logical processor.
It preserves Busy, Idle, Unknown, Missing, and Overlap seconds separately.
It does not clamp invalid percentages into the range 0 to 100.
Any missing processor, overlap, or unknown interval prevents a complete-bin result.
Inputs are limited to four million intervals and 256 processor indices.
Processor-group behavior above 64 logical processors remains NOT TESTED.

## Qualification boundary

The SDK raw ContextSwitchIn exposes IsKnownActualValue.
Its correlated IContextSwitchIn interface does not expose that property in version 1.12.10.
A direct-property compile experiment failed with CS1061 and was restored before publication.
Matching correlated boundaries do not independently prove every raw switch-in semantic.
DPC and ISR accounting is not independently qualified by the current profile.
WPA Exporter agreement remains NOT TESTED.
The new reference sets SemanticsQualified=false.
QualifiedReference, Precision, Recall, and pressure truth are withheld.
Legacy sampled outputs remain available for audit, but they are not promoted into accepted ground truth.

Read CPU_REFERENCE_RESULT.txt and CPU_GAP_DIAGNOSIS.json.
Use the official SDK weight contract and CPU analysis sources from OPEN_SOURCE_EVALUATION.

## Historical calculation boundary

The retained sampled formula excludes Process.Id zero.
That exclusion does not independently establish complete Idle accounting.
WO-10 did not modify this formula.
Fresh low-load observations and historical peak observations were below 100 percent.
Historical summary wording conflicts with retained measurements. Independent semantics remain PARTIAL.


## Baseline narrative correction

WO-09 trials 1-3 used sustained CPU workloads and returned near-100-percent sampled medians.
Peak trials 4-6 returned medians of 1.72558, 28.35959375, and 1.8503375 percent.
The retained JSON contradicts the work-order premise that all six estimates were near 100 percent.
The original evidence remains unchanged.
WO-10 does not claim a proven universal 100-percent calculation defect or its repair.
