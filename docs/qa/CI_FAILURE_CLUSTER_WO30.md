# PC-Care — WO-30 historical Windows CI failure cluster

## Scope and provenance

This report examines the 26 failed historical Windows `pull_request` workflow runs for commit `74d62eaab3ce8e45fca5f0c35432b21c09e346ab`. The companion read-only CLI and JSON evidence are in this work package.

- GitHub Actions run API: 27 runs, including 26 failed historical Windows workflows and one successful independent QA workflow.
- The failed job logs were inspected individually through the GitHub connector. Each had **128 emitted `CsWinRT1032` diagnostics**, corresponding to **64 unique source locations in 23 C# filenames** within that job.
- Total across 26 failed jobs: **3,328 emitted compiler diagnostic lines**. This is repeated output, not 3,328 distinct defects.
- All 26 jobs failed at an app build step: `Release build` in 24, `Release x64 build` in WO-02, and `Build solution` in WO-03. Tests did not run after these failed builds.
- Every inspected failed job log exposed `CsWinRT1032` as its only compiler error code. Raw logs were not committed.
- This report does **not** establish that all 64 source positions are identical across every job. It records the unique position count per job.

Examples include `src/SmartPerformanceDoctor.App/Services/MacIconCatalog.cs:10`, `src/SmartPerformanceDoctor.App/Platform/BuiltInProductModules.cs`, `src/SmartPerformanceDoctor.App/Services/DriverAudioSystemRepairIntelligenceService.cs`, `src/SmartPerformanceDoctor.App/Services/EngineClient.cs`, and `src/SmartPerformanceDoctor.App/Views/AdvancedCenterPage.xaml.cs`.

## Diagnostic meaning

`CsWinRT1032` reports collection expressions whose target types are non-mutable collection interfaces such as `IReadOnlyList<T>` or `IEnumerable<T>`. The WinRT source generator cannot guarantee a concrete representation suitable for WinRT/AOT compatibility in these contexts. This is a compiler/source-generator compatibility failure, not proof of a runtime failure.

GitHub's comparison from WO-29 tested source `e638ca51ca0e12082df0d00dffb037a583e097c0` to the failing QA PR commit shows **no `src/**` changes**. The successful WO-29 Windows CI [run 45535219952](https://github.com/scarlel96-design/PC-Care/actions/runs/45535219952) used .NET SDK **10.0.402**, explicitly pinned. Inspected failed WO-23 and WO-02 logs show their workflows' floating **`10.0.x`** selection resolved to **10.0.404**. The same Windows runner image release appeared in the compared jobs.

**Hypothesis, not established causation:** The SDK version drift or changes in generator/analyzer invocation contribute to the new diagnostics. The two builds were not run under a controlled A/B test. Other differences in workflow settings, restore targets, or pull-request checkout must still be evaluated.

## Codex-targeted follow-up without conflict

1. Reproduce a complete Release x64 build from the **same exact source commit** using pinned SDK `10.0.402` and `10.0.404` in separate clean workspaces.
2. Record SDK, compiler, Windows App SDK, CsWinRT generator/analyzer versions, effective MSBuild properties, command arguments, and both build results.
3. If the newer SDK exposes real source incompatibilities, make **targeted** `IReadOnlyList<T>`/`IEnumerable<T>` collection expression corrections using explicit concrete collection types where semantics permit.
4. Preserve ordering, mutation behavior, null/empty handling, existing APIs, thread safety, test contracts, and WinRT interop rules.
5. Do not suppress `CsWinRT1032`, disable analyzers, downgrade security validation, or broadly rewrite unrelated application code to make CI green.
6. If pinning the SDK is needed as a temporary CI containment, explicitly record the version pin and retain a separate upgrade qualification check.
7. After the fix, run app Release x64, the historical Windows regression tests, and SecurityLab. Record exact command, SHA, and outputs before claiming a corrected build.

## Tool usage

```sh
node --test tests/chatgpt-qa/ci-failure-triage.test.mjs
node tools/chatgpt-qa/ci-failure-triage-cli.mjs evidence docs/qa/CI_FAILURE_EVIDENCE_WO30.json
node tools/chatgpt-qa/ci-failure-triage-cli.mjs log path/to/a/local-github-job.log
```

The tool reads local files only. It extracts diagnostic code, relative source path, line, column, duplicate count, and SDK version when present in the log. The committed evidence contains summarized counts and stable links, not raw logs or credentials.

**Limits:** The evidence is a manually transcribed, source-linked inspection record. No raw log hashes or independent Windows toolchain reproduction are available. The CLI is not a substitute for GitHub Actions verification, signed installation, or product Phase 1 GO. This branch does not modify `src/**`, WO-30 Codex files, production gates, or `main`.
