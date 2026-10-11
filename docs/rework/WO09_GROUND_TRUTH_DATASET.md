# WO-09 ground truth protocol

STATUS: PARTIAL. This protocol was fixed before WO-09 independent CI measurements.

## Predeclared acceptance criteria

1. Use three sustained CPU trials and three short-peak trials.
2. Record a 30-second maximum workload and collector window.
3. Exclude the first two workload seconds from pressure qualification.
4. Compare only the intersection of trace, workload, and PC-Care windows.
5. Require at least ten seconds in the common window.
6. Require eight valid PC-Care observations for classification.
7. Require 80 percent reference coverage and valid one-second bins.
8. Reject any native event loss, lost buffer, CPU gap, or unknown loss count.
9. Require 90 percent CPU activity in 80 percent of reference bins.
10. Require eight consecutive high-activity bins for sustained pressure.
11. Target CPU median absolute error at ten percentage points or less.
12. Target zero false classifications in qualified CPU trials.
13. Require both positive and negative qualified trials before aggregate Precision/Recall.
14. Preserve Unknown and InsufficientEvidence as excluded observations.
15. Report failure when the predeclared target is missed.

WorkloadExecuted only proves that the bounded worker completed. PressureVerified requires independent trace measurements.
Memory allocation, cached disk operations, and mixed workload completion do not qualify pressure truth.
The WO-08 eight-workload harness remains a workload smoke test. It does not supply independent memory or disk truth.

## Isolation and privacy

Run the reference harness only on an explicitly selected GitHub Actions runner.
Store ETL and raw storage identities in private, access-controlled directories.
Keep ETL until independent analysis succeeds. Retain failed traces only for the remaining runner job lifetime.
Publish anonymized numeric reports and trace hashes. Do not publish raw ETL.
Record trial order. CPU trials precede peak trials. Cache and thermal order effects remain unqualified.

## Interpretation

This dataset qualifies CPU observations on one hosted runner. It does not prove a program's root cause.
Memory pressure, physical disk pressure, inaccessible processes, and actual PID reuse need separate ground truth.
