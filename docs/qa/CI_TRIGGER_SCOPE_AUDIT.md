# CI trigger scope audit — QA-only pull requests

## Verified starting point

At `74d62eaab3ce8e45fca5f0c35432b21c09e346ab`, the `.github/workflows` folder contains 30 YAML workflows. WO-02 through WO-27 have 26 unrestricted `pull_request` triggers. The independent QA workflow is scoped to GPT-owned files. WO-28 and both WO-29 workflows do not have a `pull_request` trigger.

For a QA-only PR, the event configuration can schedule 27 workflows. This is a configuration-derived upper count, not a count of billable runner minutes or a guaranteed run count. Workflow dispatch, branch protection, GitHub event limitations, and jobs may affect execution.

## Change

Each of the 26 historical WO-02 through WO-27 workflows keeps its original `push`, `pull_request`, and manual dispatch events and the complete Windows job. Only the `pull_request` event gains four `paths-ignore` entries:

- `tools/chatgpt-qa/**`
- `tests/chatgpt-qa/**`
- `docs/qa/**`
- `.github/workflows/chatgpt-qa.yml`

A PR containing only these QA files skips the 26 historical workflows. The independent QA workflow remains selected, so the modeled QA-only fan-out becomes 1. A PR that changes any other path, including product code, still triggers all historical PR workflows. Existing `push` and `workflow_dispatch` triggers are unchanged.

## Verification

Run `node --test tests/chatgpt-qa/core.test.mjs tests/chatgpt-qa/workflow-trigger-audit.test.mjs`.

Run `node tools/chatgpt-qa/workflow-trigger-audit.mjs .`.

The tool reads workflow files and does not modify them. It checks for all 26 named historical workflows and all four exclusions. It rejects missing and malformed configurations.

## Release and merge caution

GitHub may leave required checks Pending when workflows are skipped by path filters. Check branch protection and required status checks before approving this change. The branch protection endpoint was not readable from the current connector session. Do not assert there are no required checks. Leave this as a draft PR until an authorized reviewer confirms required checks will not block QA-only PRs.

For regular product PRs, unchanged source checks remain enabled. Do not extend the ignore list to `src/**`, system diagnostics, vault, update engine, SecurityLab, or product tests.

This change does not establish product Phase 1 GO, Windows installation qualification, actual runner-minute savings, or complete GUI accessibility.

Reference: https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax
