# WO28 - System diagnostics comparison

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

## Reference roles

Process Explorer exposes process ownership, handles and loaded modules. Process Monitor records filesystem, registry, process and thread activity. Autoruns enumerates startup locations. They provide complementary investigation data; they are not validated ground truth for every PC-Care diagnosis. [Official process utilities](https://learn.microsoft.com/en-us/sysinternals/downloads/process-utilities).

PowerToys is a modular productivity suite. Its developer interface defines module configuration and enable/disable lifecycle hooks. These hooks are architecture references, not a general transaction or rollback guarantee. [Module interface](https://github.com/microsoft/PowerToys/blob/1b33221dfa19f30d71409eb550807883fb27283a/doc/devdocs/modules/interface.md).

## PC-Care source comparison

`BottleneckAnalyzer.Analyze` distinguishes insufficient evidence, transient pressure, sustained candidate pressure and expected high load. It states that resource consumption alone is not abnormal. `RecoveryPlanner` produces review-only plans and unknown expected effects.

| Capability | Classification | Evidence needed |
|---|---|---|
| CPU/memory/disk evidence | 부분 구현 | Independent qualified reference and aligned measurement windows. |
| Cause attribution | 추가 근거 필요 | Confirm process lifetimes and workload contribution against independent traces. |
| Safe plan separation | 부분 구현 | Preserve review-only eligibility and production execution gate tests. |
| Detailed handle/module investigation | 추가 근거 필요 | Audit existing coverage before adding APIs. |
| Startup location investigation | 개선 필요 | Prefer official APIs and explicit read-only evidence over automatic disabling. |
| Modular feature state | 개선 필요 | Trace module capability, lifecycle, cancellation and UI status through existing services. |
| Resource efficiency | 추가 근거 필요 | Run matched workloads and dedicated measurement processes. |

## Test protocol

Use synthetic workloads and explicit consent. Align PC-Care and reference observation windows. Record event losses and inaccessible processes. Preserve Unknown and InsufficientEvidence outside normal/abnormal denominators. Do not infer diagnostic accuracy from event count or passing unit tests.

Measure startup, collection, cancellation and idle overhead separately. Do not combine reference-tool cost with product residency. No product speed, usability or accessibility ranking is established.
