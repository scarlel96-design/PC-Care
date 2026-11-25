# Independent PC-Care QA CLI

This Node.js-only tool inspects existing code and evidence without writing product files.

## Commands

- `node --test tests/chatgpt-qa/core.test.mjs`: run deterministic QA unit tests.
- `node tools/chatgpt-qa/cli.mjs wo29 --root .`: inspect actual WO-29 source and evidence.
- `node tools/chatgpt-qa/cli.mjs impact --codex-files <relative-txt> --qa-files <relative-txt>`: detect changed-path collisions and ownership violations.
- `node tools/chatgpt-qa/cli.mjs ui --paths <relative-txt>`: review XAML candidate patterns only.
- `node tools/chatgpt-qa/cli.mjs premerge --checks <relative-json>`: evaluate integration prerequisites.
- `node tools/chatgpt-qa/cli.mjs hash --file <relative-path>`: calculate SHA-256 over local file bytes.

Use UTF-8, repository-relative paths. Run from the checkout root.

The checks do not write source files. They do not execute untrusted programs, restore files, access security credentials, or authorize product deployment.

## Limits

The XAML scanner cannot determine actual usability. The dependency scan cannot find vulnerabilities online. The diagnostic cases are synthetic data only. The evidence checker compares reported values and common source references; it does not authenticate a remote CI run or reproduce Windows execution.

See `docs/qa/WO30_GPT_INDEPENDENT_WORKPACK.md` for task ownership and acceptance contracts.

## WO-30 hardened integrated runner

Run `node tools/chatgpt-qa/integrated-runner.mjs --root . --profile offline` for the local evidence and test suite. Use `--profile checkout --strict` for the complete available checkout inspection. A missing prerequisite or warning fails strict mode. See `docs/qa/WO30_QA_HARDENING.md` for test inventory, contracts and unresolved release gates. Never treat the local premerge output as approval.
