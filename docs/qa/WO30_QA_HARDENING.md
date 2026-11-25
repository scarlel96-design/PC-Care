# WO-30 independent QA hardening

## Disposition

This revision addresses all 14 findings in the deep review of `9533e3888db13291f48e7cdd96ce84ac14c30991`. The 21 previously failing negative acceptance tests are now permanent regression tests. An additional 46 boundary tests exercise variants of those findings. No product source, product test, or historical Windows workflow is changed. The old deep review and final reports remain historical evidence.

## Corrections

| ID | Correction |
| --- | --- |
| R01 | Mask C# comments and strings. Require one complete active Av3PhaseGate class. Reject conditional compilation, duplicate flags, nonliteral false and detectable reassignment. |
| R02 | Require the exact 26 historical filenames and exact four allowed legacy PR exclusions. Reject additional/duplicate exclusions, unsupported PR filters and unsafe paths. |
| R03 | Recognize only a supported unconditional SDK setup, literal global.json write and matching terminating assertion before the first build/restore. Reject uncertain order, conditions, working directory, shell and later pin changes. Require both successful and failed observations for a comparison. |
| R04 | Check nine contributing task statuses. Treat any supplied QA10 result as legacy self-report. Never return merge-ready from caller assertions. Independent authentication and human review remain mandatory. |
| R05 | Recognize both QA-owned workflows. Reject invalid, repeated, traversal, reserved Windows names and case-aliased paths. |
| R06 | Validate diagnostic categories and states before indexing. Use null-prototype counters. |
| R07 | Inspect active QA workflow event/permission/path keys and required unconditional run steps. Reject unsupported or duplicate root keys and write overrides. Run the complete test inventory from QA CI. |
| R08 | Parse a bounded MSBuild XML subset with matching tags and quoted attributes. Reject DTD, malformed declarations, ambiguous conditional packages and floating versions. |
| R09 | Require positive observed error counts, expected failure coverage, unique job identity and consistent counters before identifying a common error. |
| R10 | Deduplicate runs/jobs, reject invalid identities and mismatched totals, exclude jobs from another run, and mark unobserved previous attempts partial. |
| R11 | Report nested interface-return collection expressions as review candidates, without claiming semantic certainty. |
| R12 | Propagate advisory/empty coverage and unknown statuses. Reject self-reported approval. Block empty suite summaries. |
| R13 | Ignore commented XML. Inspect real package/project declarations. Treat no-baseline checkout as INVENTORIED, and missing observations as INCOMPLETE. |
| R14 | Require 11 named test files, a reviewed SHA-256 inventory and at least 344 tests. Check hashes before running the complete suite. Block missing files, changed hashes, invalid summaries or no tests. Prevent report overwrites. |

The legacy path-scope proposal remains separate: PR #3 still contains the older analyzer and 26 unmerged historical workflow changes. This revision supplies the replacement analyzer in the independent QA branch, not approval of PR #3. Do not merge the old draft. Confirm required status checks and replace its old analyzer before any future authorized integration. The four historical exclusions remain conservative; edits to the new manual integration workflow still request legacy validation under that proposal.

## Verification

- Node.js v22.16.0. `node --test tests/chatgpt-qa/*.test.mjs`: 344 PASS, 0 FAIL, 0 SKIP across 11 files. 277 original test cases retained; 21 former negative tests and 46 new boundary cases. Some original assertions now enforce the revised contracts (approval always blocked, inventory without baseline, missing test coverage blocked). The tests were not discarded to obtain a passing result.
- `node --check` for every QA module and test file: PASS.
- Offline integration: 4 PASS, 1 WARN, 8 SKIPPED; overall PARTIAL, exit 0. Strict mode: PARTIAL, exit 1. Focused unit-tests mode: unit-tests PASS, exit 0, other checks SKIPPED.
- `QA_TEST_INVENTORY_WO30.json` contains the reviewed test hashes (UTF-8 normalized LF). Changing a required test requires deliberate review and manifest update; the manifest is not an independent cryptographic attestation of remote execution.

## Use and limits

From a trusted checkout, run `node tools/chatgpt-qa/integrated-runner.mjs --root . --profile checkout --strict`. Supply actual source, changed-path, UI and approval evidence when required. Do not supply invented values. Write new reports to unused paths under `docs/qa/`.

The C# and workflow inspectors are conservative static heuristics for supported syntax, not Roslyn, a general YAML parser, or GitHub's event engine. The XML reader is a restricted declaration parser, not evaluated MSBuild. The corrections establish the tested cases, not absence of every future defect. Full source checkout validation, hosted CI of this revision, all 64 actual compiler locations, controlled SDK A/B Windows builds, signed installation, UAC, reboot, GUI and physical interruption remain unverified here. No automatic merge or production approval is permitted. Phase 1 remains NO-GO.
