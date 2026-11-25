# WO-30 — GPT independent QA integration (task 09/10)

## Scope and ownership

This integration runs existing GPT QA inspectors from one local Node.js entry point. It does not modify product code, historical Windows CI, security policy, AV3 gates, or release decisions. The implementation is isolated to `tools/chatgpt-qa/`, `tests/chatgpt-qa/`, `docs/qa/`, and a new manual workflow.

Reference branch at start: `chatgpt/pccare-wo30-build-env-provenance`. Reference SHA: `c5d5c8e7cbff9cc586c363213e754bb50b3badc2`.

## Usage

Run offline checks without a full source checkout:

```sh
node tools/chatgpt-qa/integrated-runner.mjs --root . --profile offline
```

Run all available source-level inspections in a complete checkout:

```sh
node tools/chatgpt-qa/integrated-runner.mjs --root . --profile checkout --output docs/qa/qa-integrated-report.json
```

Run a focused check:

```sh
node tools/chatgpt-qa/integrated-runner.mjs --only ci-failure-evidence
```

Provide optional Codex ownership, UI, and premerge files:

```sh
node tools/chatgpt-qa/integrated-runner.mjs --profile checkout \
  --codex-files docs/qa/codex-changed-files.txt \
  --qa-files docs/qa/gpt-changed-files.txt \
  --ui-paths docs/qa/xaml-paths.txt \
  --premerge-checks docs/qa/premerge-input.json
```

These paths are examples. They are not included in this package. Use real, verified data for each optional input. Reject missing or escaped file paths.

Use `--list` to inspect the check registry. Use `--strict` to fail when any check is not `PASS`. Use `--source-sha <40-character SHA>` only when the SHA is independently known. Do not infer the product SHA from a documentation baseline.

## Check registry

| ID | Check | Evidence classification |
| --- | --- | --- |
| `unit-tests` | Run all independent QA Node tests | LOCAL_EXECUTION |
| `ci-failure-evidence` | Check existing 26-job error evidence | HISTORICAL_GITHUB_SUMMARY |
| `sdk-run-compare` | Compare observed SDK installations | HISTORICAL_GITHUB_SUMMARY |
| `build-env-run-compare` | Compare observed build environments | HISTORICAL_GITHUB_SUMMARY |
| `ci-observability-synthetic` | Validate a synthetic GitHub Actions snapshot | SYNTHETIC_FIXTURE |
| `wo29-source-evidence` | Read original WO-29 evidence and security gates | LOCAL_CHECKOUT |
| `sdk-workflow-drift` | Compare workflow SDK selectors to the saved baseline | LOCAL_CHECKOUT |
| `build-env-checkout` | Inspect project packages and workflow configuration | LOCAL_CHECKOUT |
| `cswinrt-source-guard` | Scan C# for CsWinRT1032 risk patterns | LOCAL_CHECKOUT |
| `workflow-trigger-scope` | Inspect legacy PR trigger filters | OPTIONAL_BRANCH_TOOL |
| `change-impact` | Compare Codex and GPT changed paths | USER_SUPPLIED |
| `ui-static-review` | Inspect explicitly selected XAML files | USER_SUPPLIED |
| `premerge-readiness` | Inspect the complete ten-task approval evidence | USER_SUPPLIED |

`workflow-trigger-scope` is available only after the separate PR #3 tool is present in the target checkout. This package does not silently copy the PR #3 files.

## Status semantics

- `PASS`: The individual tool passed its stated inspection. This never means product Phase 1 GO.
- `WARN`: The check completed with known findings, incomplete evidence, or a comparison requiring investigation.
- `BLOCKED`: The check found a failing gate or regression.
- `SKIPPED`: The check was not run. The result includes its missing prerequisite.
- `ERROR`: The tool failed to execute or produced invalid evidence.

The combined result is `BLOCKED` for any blocked check or tool error. Otherwise, it is `PARTIAL` when a check is skipped. It is `REVIEW` when every check ran but one or more returned a warning. Only a complete, clean check set gets `PASS`.

Exit code is 1 for a blocked suite. Use `--strict` to also fail on `PARTIAL` or `REVIEW`. An exit code of 0 for a partial suite confirms only that no executed check blocked; it does not authorize a release.

## Verification

The source assembly from the published GPT QA work packages ran 277 Node.js tests with 277 PASS, 0 FAIL, 0 SKIP. This includes 55 new integration tests and 222 prior tool tests. These are local tests, not hosted GitHub Actions results.

An offline integrated run executed 5 checks: 4 PASS, 1 WARN, and 8 SKIPPED. `build-env-run-compare` returned `REVIEW` for the known `10.0.402` versus `10.0.404` installed SDK difference. The result was recorded as `WARN` without asserting causality.

The aggregated result is `PARTIAL`. Product release readiness is always `NOT_ASSESSED`. The runner does not claim that historical evidence has been re-fetched or that signed installation, UAC, reboot, VM, GUI, or SecurityLab Windows E2E testing was performed.

## CI workflow behavior

`.github/workflows/chatgpt-qa-integration.yml` is *manual only*. It adds no `push` or `pull_request` trigger. It uploads the JSON report as an artifact if someone explicitly dispatches the workflow. GitHub generally requires a workflow file on the default branch for manual dispatch to be available. No workflow dispatch is claimed by this local verification.

PRs #1, #2, and #3 remain separate. Do not merge this branch or revise product qualification rules automatically. Before integration, compare the latest Codex change list and repeat the full checkout tests.

## Known limitations

The offline runner does not validate the product source. A complete source checkout is required for source-level checks. UI, file overlap, and premerge status require external inputs. Known historical errors and static warnings remain warnings, not fabricated passes. The suite itself does not download GitHub logs or depend on credentials.
