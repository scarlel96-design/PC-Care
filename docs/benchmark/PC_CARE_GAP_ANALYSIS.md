# WO28 - PC-Care gap analysis

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

## Classification contract

Allowed classifications are `PC-Care가 더 우수`, `동등`, `개선 필요`, `부분 구현`, `미구현`, `도입 가치 없음`, and `추가 근거 필요`.

No comparative superiority or equivalence rating is supported by this static review. A partial implementation does not imply production qualification.

| ID | Reference and concrete lesson | PC-Care finding | Classification | Proposed benefit |
|---|---|---|---|---|
| G01 | Cryptomator separates config recovery from lost content | Existing receipts and salvage need user-facing distinctions | 개선 필요 | Reduce false recovery expectations. |
| G02 | Recovery key semantics are explicit | PC-Care consumable codes must not be described as an equivalent stable master-key recovery secret | 개선 필요 | Clarify credential and consumption risks. |
| G03 | VeraCrypt presents header backups and credential history | Authenticated backup freshness and supported format need explicit presentation | 부분 구현 | Improve restoration decisions without weakening authentication. |
| G04 | BleachBit separates provider definitions and preview | PC-Care currently limits read-only providers to UserTemp | 개선 필요 | Expand verified observation coverage. |
| G05 | CleanerML can launch processes and modify registry | Importing its full action language would exceed PC-Care's read-only boundary | 도입 가치 없음 | Preserve execution authority separation. |
| G06 | SDelete explains allocation-specific scope | PC-Care has explicit media limitations and unsupported-file checks | 부분 구현 | Keep visible claims aligned with logical Clear evidence. |
| G07 | PowerToys exposes module lifecycle and settings restore policy | PC-Care services already exist; avoid another plugin engine | 개선 필요 | Improve consistent capability and error state display. |
| G08 | Independent reference tools expose different views | CPU ground truth and actual GUI still need qualification | 추가 근거 필요 | Support valid accuracy and UX conclusions. |
| G09 | WO27 history pins reduce handles | Managed allocations increased in the recorded CI comparison | 개선 필요 | Target allocations without introducing TOCTOU. |
| G10 | WO27 preserves 24 unknown partial states | Complete residual inventory and reconciliation retermination remain open | 부분 구현 | Explain persisted evidence and preserve uncertain data. |
| G11 | Protected cross-volume staging has extra failure states | A/B crash and reconciliation durability are unqualified | 부분 구현 | Establish future publication prerequisites. |

Source details and primary URLs appear in the five comparison documents. These are independent design proposals. No competitor code, engine, driver or rules were adopted.

## Decision

Prioritize G10, G01/G02, G09 and G04. Keep real signed deployment, privileged recovery, GUI, device and independent freshness P0 open. Phase 1 remains NO-GO.
