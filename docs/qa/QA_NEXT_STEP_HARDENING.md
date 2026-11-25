# WO-30 GPT QA follow-up: pre-merge and CI hardening

## Scope

This follow-up modifies only GPT-owned QA files. It does not change product code, WO-29 evidence, production security gates, or the Codex work branch.

## Defect QA-10: incomplete audit sets were accepted

Before this change, the pre-merge checker accepted a single `PASS` audit if the three supplied prerequisite flags were true. That result did not cover the ten selected QA tasks.

The checker now requires all ten unique `QA_TASKS` identifiers. It rejects unknown or duplicate IDs and invalid statuses. Missing audits produce `BLOCKED`. Failed audits or malformed sets produce `FAIL`. `PARTIAL`, `NOT_TESTED`, and `BLOCKED` audits cannot produce `PASS`.

The three prerequisite flags are caller assertions. They do not authenticate the Codex Git ref, the local test run, or GitHub Actions. The output therefore includes `reviewOnly` and `externalClaimsNotVerified`. A `PASS` applies only to these supplied inputs. It is not product GO or a merge authorization.

## Inefficiency QA-08: duplicate QA workflow triggers

The independent QA workflow previously had both `push` and `pull_request` triggers. A push to a branch with an open PR could start duplicate QA runs.

The workflow now uses `pull_request` and `workflow_dispatch` only. The concurrency group cancels obsolete runs for the same pull request or manual ref. The workflow still restricts automatic execution to GPT-owned paths.

**Limit:** This change can prevent redundant executions of the **ChatGPT independent QA** workflow. It does not disable or reduce unrelated repository workflows. The previously observed repository-wide workflow fan-out requires a separate owner-approved review.

## Reproduction commands

Run these commands from a checkout that includes the QA files:

```powershell
node --check tools/chatgpt-qa/core.mjs
node --check tests/chatgpt-qa/core.test.mjs
node --test tests/chatgpt-qa/core.test.mjs
```

Expected result: 36 Node tests pass. Any failed test is a blocker.

An integration run is **not** complete until the independent QA workflow succeeds on the updated pull request and the latest Codex changed-file list is compared.

## Safety and handoff

Do not change the production AV3 flags. Do not merge this draft PR automatically. Continue to report Windows VM, UAC, reboot, and real GUI qualification as `NOT TESTED` until actual evidence exists. Keep Phase 1 as `NO-GO`.
