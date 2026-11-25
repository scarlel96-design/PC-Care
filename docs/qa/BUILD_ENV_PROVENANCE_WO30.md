# PC-Care WO-30 — build environment provenance audit

## Purpose

Track build environment changes separately from product changes. Provide Codex with source-linked evidence before assigning a CI failure to SDK, compiler, or dependency drift.

## Source baseline

GitHub branch `chatgpt/pccare-wo30-sdk-drift-audit`, commit `cfd99e7e78350bdead91eef279e67d3ae83d84ac`.

- 26 historical WO-02–WO-27 Windows workflow configurations use `dotnet-version: '10.0.x'`.
- WO-28 and two WO-29 workflows use exact `10.0.402` setup and scripted `global.json` with `rollForward: disable`.
- WO-28 and WO-29 qualification workflows also assert the selected SDK version. WO-29 staging does not.
- `Directory.Packages.props` declares `Microsoft.WindowsAppSDK` version `2.2.0` and `Microsoft.Windows.CsWin32` version `0.3.358`.
- `SmartPerformanceDoctor.App.csproj` targets `net10.0-windows10.0.26100.0` and references `Microsoft.WindowsAppSDK`.
- The root `global.json` was not found in the inspected repository branch.

These facts are *configuration observations*. The resolved Windows App SDK, transitive CsWinRT generator, and Roslyn compiler versions remain **UNKNOWN** without restored assets or explicit build logs.

## GitHub Actions evidence

The accompanying evidence file records three actual runs. A successful WO-29 qualification job installed SDK `10.0.402`; failed WO-23 and WO-02 jobs installed `10.0.404`. Their runner release tag was `win25-vs2026/20261116.315`. The failing logs reported `CsWinRT1032`. The WO-29 and WO-23 build command lines matched; WO-02 used a project build with additional flags.

**Do not assert causality.** The successful and failed runs used different full commits and potentially different other build conditions. A prior GitHub comparison found no product source changes, but that alone is not a controlled A/B test.

## Tool and usage

Use Node.js 22 or newer. The tools only read checkout files and supplied logs.

1. Run the unit tests:

   `node --test tests/chatgpt-qa/build-env-provenance.test.mjs`

2. Inspect a checkout:

   `node tools/chatgpt-qa/build-env-cli.mjs audit --root .`

3. Capture a local baseline as JSON using shell redirection:

   `node tools/chatgpt-qa/build-env-cli.mjs baseline --root . > build-env-baseline.json`

4. Compare the checkout against the saved baseline:

   `node tools/chatgpt-qa/build-env-cli.mjs audit --root . --baseline build-env-baseline.json`

5. Inspect a saved job log:

   `node tools/chatgpt-qa/build-env-cli.mjs log --file job.log`

6. Compare two explicitly recorded observations:

   `node tools/chatgpt-qa/build-env-cli.mjs compare --file comparison.json`

For comparison, supply an object containing `left` and `right` observations. Use the keys in `compareBuildEnvironments`: `sourceSha`, `installedSdk`, `selectedSdk`, `runnerImage`, `compilerVersion`, `msbuildVersion`, `windowsAppSdkDeclared`, `windowsAppSdkResolved`, `csWinRtGeneratorVersion`, `csWin32Declared`, and `buildCommand`.

A change in a known value returns `REVIEW`; absent observations return `INCOMPLETE` unless another known value changed. The result never claims root-cause proof.

## Verification boundaries

Local tests exercise synthetic workflow, project, and log fixtures. The full PC-Care repository was inspected via GitHub but was not cloned for a complete checkout audit. The `audit` command has not been run against a full real checkout. The job-log examples were inspected via the GitHub connector. A full Windows 10.0.402-vs-10.0.404 clean build comparison is **NOT TESTED**.

The tool uses narrow YAML and XML pattern extraction, not full parsers or MSBuild evaluation. It cannot resolve conditional SDK selection, inherited props, transitive package versions, compiler task assemblies, or exact runtime toolchain without additional artifacts.

## Codex acceptance gates

- Run the same source SHA on Windows with SDK `10.0.402` and `10.0.404` in independent clean workspaces.
- Record `dotnet --info`, `dotnet --version`, runner image release, full build command, and restore lock/asset provenance.
- Extract exact `Microsoft.WindowsAppSDK` and CsWinRT package versions from `obj/project.assets.json` or a binlog.
- Capture compiler and MSBuild versions from explicit output.
- Compare error codes and diagnostic locations without suppressing analyzers or reducing security checks.
- Preserve all AV3 production NO-GO gates. Leave Phase 1 NO-GO until separate release conditions pass.

No product source, Windows workflow, `main`, or Codex WO files are modified by this work package.
