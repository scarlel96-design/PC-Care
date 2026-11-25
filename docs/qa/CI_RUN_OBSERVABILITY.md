# CI execution observability: independent QA task 04

## Scope

Inspect GitHub Actions run history for one exact commit SHA. Measure completed job wall time, workflow fan-out, repeated-run candidates, and failure steps.

This tool does not modify PC-Care source files, workflows, GitHub settings, or stored GitHub Actions runs. It uses only Node.js built-in modules. It does not invoke Codex or require a Windows VM.

## Reproducible offline test

From the repository root, execute:

```sh
node --test tests/chatgpt-qa/ci-observability.test.mjs
node tools/chatgpt-qa/ci-observability-cli.mjs --input tests/chatgpt-qa/fixtures/ci-synthetic-snapshot.json
```

The fixture is **synthetic**. Never cite its numbers as actual CI measurements.

## Fetch the actual run and job history

Set `GITHUB_TOKEN` for higher REST API limits. Use a token with read-only access to Actions metadata for private repositories. Do not print or commit this token.

```sh
node tools/chatgpt-qa/ci-observability-cli.mjs \
  --repository scarlel96-design/PC-Care \
  --sha 74d62eaab3ce8e45fca5f0c35432b21c09e346ab \
  --with-jobs \
  --max-runs 100 \
  --output ci-analysis-local.json
```

The command reads the list of workflow runs and the first 100 jobs for each selected run. It caps the run count at 200. Job requests use a concurrency limit of four. If the API reports more runs or jobs than retrieved, the result is `PARTIAL`.

Without `--with-jobs`, job durations are unavailable. Their value is `null`, not zero. Active runs, invalid timestamps, missing jobs, and incomplete pagination prevent a `COMPLETE` result.

To compare two captured GitHub REST snapshots, execute:

```sh
node tools/chatgpt-qa/ci-observability-cli.mjs \
  --input before-snapshot.json \
  --compare after-snapshot.json \
  --output comparison-local.json
```

`--input` requires the standard GitHub `workflow_runs` array plus an optional `jobs_by_run` mapping. A mapping entry contains `total_count` and the `jobs` array returned by the jobs endpoint. A full comparison requires complete snapshots with valid completed-job timestamps.

## Measurements

- **Workflow fan-out:** number of run records and distinct workflow IDs for one SHA and event.
- **Repeated-run candidate:** separate run IDs with the same workflow ID, SHA, and event. This pattern is not proof of wasted work.
- **Rerun attempts:** `run_attempt - 1` for each observed run.
- **Observed job wall time:** sum of completed job `completed_at - started_at` values. Parallel jobs overlap in real elapsed time.
- **Observed queue wait:** `started_at - created_at`, when both timestamps exist.
- **Failure step count:** number of job steps with `conclusion=failure`, grouped by step name.
- **Billing minutes:** **NOT AVAILABLE**. No price, savings, or billable-minute figure is estimated.

## Confirmed WO-30 baseline observation

See [`CI_RUN_BASELINE_20261125.json`](CI_RUN_BASELINE_20261125.json). It records 27 distinct run IDs for commit `74d62eaab3ce8e45fca5f0c35432b21c09e346ab`, with one completed job per run. The job timestamps yielded **3,552 observed wall seconds** (59.2 summed job-minutes). The 26 legacy Windows workflows ended with failure and consumed **3,538 wall seconds** in total. The independent QA workflow succeeded in **14 seconds**.

The 26 failures are not automatically attributable to the trigger configuration. Two sampled failing Windows jobs both failed at the `Release build` step. Root-cause analysis of all failures was not part of this work.

These measurements describe the execution *before* any approved workflow path-filter integration. They do not demonstrate a realized reduction after PR #3.

## Integration rule

Keep this change separate from the Codex WO-30 product branch and the historical-workflow trigger modifications in PR #3. Publish it on its own QA branch. Avoid opening another PR before trigger scoping is approved, because existing historical workflows currently run on unfiltered PR events.

For an actual before/after effectiveness claim, rerun the tool on a new QA-only PR after trigger scoping takes effect. Use comparable change sets. Record missing data and do not extrapolate billed costs.

## Completion limitations

This work proves the analysis utility and the archived API observation. It does not prove any Windows release, installation, code-signing, UAC, vault, or recovery operation. Phase 1 remains NO-GO.
