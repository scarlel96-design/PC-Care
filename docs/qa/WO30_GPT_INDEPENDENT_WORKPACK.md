# PC-Care WO-30: GPT independent QA work package

## Goal and baseline

Keep the product implementation and independent QA work separate. Reduce repeat investigation by providing deterministic checks and review-ready evidence.

- Repository: `scarlel96-design/PC-Care`
- Baseline: WO-29 final commit `f1d4e7a4872aca8d1114183840441d687a8b5c5e`
- Windows qualification source: `e638ca51ca0e12082df0d00dffb037a583e097c0`
- WO-30 branch: not found when this package was prepared. Recheck before integration.
- Integration: draft PR only. Never auto-merge to WO-30 or `main`.
- Authenticode certificate purchase is not required. Do not weaken package integrity or file-boundary controls.
- Product release and Phase 1 remain **NO-GO** until separately qualified.

## Ownership rules

Codex owns `src/**`, product tests, functional update and recovery scripts, `docs/rework/WO30_*`, and Windows VM execution.

GPT owns only:

- `tools/chatgpt-qa/**`
- `tests/chatgpt-qa/**`
- `docs/qa/**`
- `.github/workflows/chatgpt-qa.yml`

The earlier AV3 guard in PR #1 remains independent. Do not copy its four files into this package.

Never edit production engines, security gates, or the existing WO-29 evidence. Raise a new proposal if another file requires modification.

## Ten selected tasks and acceptance contracts

| ID | Independent deliverable | Acceptance condition | Current evidence scope |
|---|---|---|---|
| QA-01 Change impact | Compare Codex and GPT path lists; reject traversal, foreign paths, and case-insensitive overlaps | No shared changed path in the reviewed comparison | Unit tested; future WO-30 paths not yet supplied |
| QA-02 Security policy | Check six AV3 flags and read-only phase status | Fail when a flag is enabled or absent | Static text verification only; complements PR #1 |
| QA-03 Test automation | Run Node built-in test suite and deterministic JSON data | Zero failed QA unit tests | Local Node execution verified |
| QA-04 Performance | Compare paired 4,096-history measurements and report medians, ranges, identity calls | Preserve call counts; never claim speedup from allocation alone | Existing WO-29 synthetic measurement |
| QA-05 Evidence consistency | Compare Windows test counts, same source SHA, Phase 1 status and report details | Mismatch causes FAIL | Source references checked, not independently authenticated source blobs |
| QA-06 UI static | Identify hardcoded XAML colors and empty button labels | Emit REVIEW candidates; do not certify accessibility | Static only; real keyboard/DPI/reader testing remains open |
| QA-07 Diagnostic fixtures | Maintain 30 synthetic cases in 10 categories and 3 expected states | Unique IDs, immutable expectations, no mutation permission | Data schema tests only; NOT product diagnostic validation |
| QA-08 CI efficiency | Run path-filtered Node QA on Ubuntu; no NuGet restore or Windows build | CI workflow contains scoped triggers and time limit | Workflow added; remote CI requires run evidence |
| QA-09 Dependencies | Flag duplicate and floating central PackageVersion entries | Fail on duplicate or non-pinned version | Text audit only; not an online vulnerability/license scan |
| QA-10 Pre-merge | Aggregate failed, partial and unverified prerequisites | Block integration unless base, tests and CI are all observed | Await new WO-30 branch and remote CI |

## Instructions

### Test the independent package

Use Node.js 22.16.0 or a compatible 22 release.

```powershell
node --check tools/chatgpt-qa/core.mjs
node --check tools/chatgpt-qa/cli.mjs
node --test tests/chatgpt-qa/core.test.mjs
```

Do not install additional packages for this QA suite.

### Audit the actual WO-29 evidence in the checkout

```powershell
node tools/chatgpt-qa/cli.mjs wo29 --root .
```

The command reads existing source, reports, performance samples, NuGet version declarations, and the QA fixtures. It does not change files. A successful command does not establish a product release GO decision.

### Check file ownership and overlap

Write separate UTF-8 text files containing one changed path per line. Use repository-relative paths. Include the current Codex WO-30 changes in the Codex list.

```powershell
node tools/chatgpt-qa/cli.mjs impact --codex-files docs/qa/codex-paths.txt --qa-files docs/qa/gpt-paths.txt
```

The two lists in the example are **inputs to prepare**, not committed results. An unavailable Codex list remains a blocker.

### Review XAML candidates

```powershell
git ls-files '*.xaml' > .chatgpt-qa-xaml-list.txt
node tools/chatgpt-qa/cli.mjs ui --paths .chatgpt-qa-xaml-list.txt
Remove-Item .chatgpt-qa-xaml-list.txt
```

Review all static candidates manually. Use an interactive Windows environment for accessibility acceptance.

### Audit one file hash

```powershell
node tools/chatgpt-qa/cli.mjs hash --file docs/qa/WO29_EVIDENCE_REFERENCE.json
```

The resulting SHA-256 identifies local file bytes. It does not validate the original Windows test log independently.

## Integration contract

1. Re-read the latest WO-30 HEAD and changed files.
2. Compare those changes against the GPT branch before requesting merge.
3. Run QA tests in a checkout of the proposed merge.
4. Examine the independent GitHub Actions result.
5. Keep any actual Windows VM, UAC, reboot or vault usability item marked NOT TESTED without real execution evidence.
6. Request human approval for merge. Do not alter `main` or publish a release automatically.

## Status vocabulary

`PASS` means the stated narrow QA check passed. `PARTIAL` means static evidence only. `NOT_TESTED` means no execution evidence exists. `BLOCKED` means an integration prerequisite is missing. `FAIL` means the stated contract failed.

This package is not an AV3 production enable request. It does not claim Windows VM qualification, physical power-loss safety, or full application feature completion.
