# PC-Care WO-30 — .NET SDK drift audit

## Purpose

Detect .NET SDK selector changes and classify historical Windows CI discrepancies. Keep the audit separate from Codex product code, signing, recovery, and release decisions.

## Verified repository baseline

GitHub source: `e430e571948515d091dcd1ab8210d2c715ae9b32`.

| Workflow scope | Count | Observed configuration |
|---|---:|---|
| WO-02 through WO-27 | 26 | `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`. No workflow `global.json` pin. |
| WO-28 and two WO-29 workflows | 3 | Exact `10.0.402`, `global.json` with `rollForward: disable`. The two qualification workflows also assert `dotnet --version`. |
| GPT independent QA | 1 | Node-only. No .NET SDK setup or build command. |

**Qualification:** WO-29 synthetic staging and vault workflow has an exact `10.0.402` setup and `global.json` pin, but **does not contain a `dotnet --version` assertion**. The static classifier therefore marks it `EXACT_NO_GUARD`, not `GUARDED_EXACT`. The two WO-28/WO-29 qualification workflows are `GUARDED_EXACT`.

The committed baseline file must reflect that distinction. The audit reports previous floating selectors as `ADVISORY`, not `PASS`. A new unreviewed floating selector, a missing established guard, or a silent change to an exact guarded SDK is `BLOCKED`.

## Actual CI evidence

The successful WO-29 job 138940048642 installed SDK `10.0.402`. Two failing historical Windows jobs, 139054659704 and 139054661095, used setup-dotnet SDK `10.0.404`. Their logs reported `CsWinRT1032` in app Release builds. These are **observed installations**, not necessarily the output of `dotnet --version` at every build invocation.

The comparison establishes an SDK version difference. **It does not prove the SDK difference caused the errors.** A controlled same-source clean A/B comparison remains necessary.

## Commands

```sh
node --test tests/chatgpt-qa/sdk-drift-audit.test.mjs
node tools/chatgpt-qa/sdk-drift-cli.mjs audit --root . --baseline docs/qa/SDK_DRIFT_BASELINE_WO30.json
node tools/chatgpt-qa/sdk-drift-cli.mjs compare --evidence docs/qa/SDK_DRIFT_RUN_EVIDENCE_WO30.json
node tools/chatgpt-qa/sdk-drift-cli.mjs log --file path/to/local-ci-job.log
```

The audit requires a checkout containing `.github/workflows`. It performs no network operations and does not modify files. The local tests use synthetic workflow fixtures. The complete repository was inspected through the GitHub connector, not cloned into this test container. Running this CLI across an actual checkout is **NOT TESTED** until someone executes that command against a materialized repository.

The classifier is a narrow, line-oriented workflow inspector. Unsupported YAML structures, action input indirection, imported reusable workflows, external shell scripts, and environment-based SDK overrides may need manual review. It does not replace an actual .NET build or a full YAML parser.

## Required Codex follow-up

1. Compare SDK `10.0.402` and `10.0.404` using the same exact product source SHA.
2. Use separate clean Windows workspaces. Retain identical build flags, dependencies, and relevant environment variables.
3. Record `dotnet --info`, `dotnet --version`, selected SDK, Windows App SDK, CsWinRT analyzer version, build command, and diagnostics.
4. If the compatibility problem is confirmed, fix source collection expressions using concrete collection types where semantics permit.
5. Preserve API contracts, collection order, mutation, cancellation, rollback, signing and AV3 production gates.
6. Do not suppress CsWinRT analyzers or claim Phase 1 GO from a green build alone.

## Restrictions

Do not automatically pin historical workflows to a selected version. Keep the existing CI qualification coverage. Do not alter `src/**`, product tests, security approval flags or `main` from this GPT branch.

References:
- [Microsoft .NET `global.json` SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [GitHub `setup-dotnet` documentation](https://github.com/actions/setup-dotnet)
