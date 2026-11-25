# CsWinRT1032 independent static guard

## Purpose

Detect selected C# collection expressions that may trigger `CsWinRT1032`. Keep the tool independent of PC-Care product code.

This scanner is **not** Roslyn. It cannot resolve full C# type semantics or prove whether a source file compiles. Findings are source-level review candidates, not confirmed compiler errors.

## Verified failure context

The WO-30 CI failure analysis inspected 26 Windows job logs from commit `74d62eaab3ce8e45fca5f0c35432b21c09e346ab`. Each job had 128 emitted `CsWinRT1032` diagnostic lines, representing 64 unique positions in 23 C# filenames. Duplicate compiler output and GitHub annotations do not constitute separate defects.

The earlier successful WO-29 Windows build pinned SDK `10.0.402`. Verified failing jobs used SDK `10.0.404` through the floating `10.0.x` selector. The SDK difference remains a **hypothesis**, not a demonstrated cause. Only a controlled same-source comparison can establish causation.

Source links:
- [WO-30 original failure report](https://github.com/scarlel96-design/PC-Care/blob/7f7c1463e75c09a697c5990b429123b778a80ea4/docs/qa/CI_FAILURE_CLUSTER_WO30.md)
- [WO-29 successful Windows CI](https://github.com/scarlel96-design/PC-Care/actions/runs/45535219952)
- [Historical Windows CI failure](https://github.com/scarlel96-design/PC-Care/actions/runs/45570191416)

## Scanning scope

Default: `src/SmartPerformanceDoctor.App` in a local PC-Care checkout.

High-confidence *syntax-risk* patterns:

- Interface-typed field or variable directly initialized with `[ ... ]`.
- Interface-typed property directly initialized with `[ ... ]`.
- Interface-typed expression-bodied property or method returning `[ ... ]`.
- Interface-returning method with a direct `return [ ... ]`.
- Local same-source method call with a direct `[ ... ]` argument at a known interface parameter index.

Supported target families: `IEnumerable<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `ICollection<T>`, and `IList<T>`.

Named arguments such as `SearchTerms: [ ... ]` are **advisory**. Their target parameter types require semantic analysis. Other constructor arguments, overload resolution across files, conditional compilation, source generators, extension methods, implicit conversions, interfaces with aliases, and nested local functions are not fully resolved.

The scanner masks comments and strings. It also skips typical `bin`, `obj`, and package directories. It rejects source paths outside the checkout.

## Commands

From the PC-Care repository root:

```sh
node --test tests/chatgpt-qa/cswinrt-static*.test.mjs tests/chatgpt-qa/cswinrt-patterns.test.mjs
node tools/chatgpt-qa/cswinrt-static-cli.mjs --root . --summary
node tools/chatgpt-qa/cswinrt-static-cli.mjs --root . --fail-high
```

For specific source files, write UTF-8 repository-relative `.cs` paths to `changed-csharp.txt`, then run:

```sh
node tools/chatgpt-qa/cswinrt-static-cli.mjs --root . --files-list changed-csharp.txt
```

The default mode **reports** findings without failing CI. `--fail-high` is an explicit strict gate. Use it only after the reviewed scope has no existing warnings.

To compare against an independently verified baseline JSON produced by this CLI:

```sh
node tools/chatgpt-qa/cswinrt-static-cli.mjs --root . --baseline docs/qa/reviewed-baseline.json --fail-new
```

A baseline file is **not provided** in this change. Do not silently approve existing defects by creating one. The comparison uses path, line, column, and rule. Line movement can produce conservative false-positive changes. Inspect such findings before approving.

## Safety, ownership, and CI

The tool reads C# source. It never edits application code, changes SDK versions, disables analyzers, or enables vault production gates. The CLI prints JSON and exit codes. It does not call GitHub or run dotnet.

The test fixtures reproduce **six observed syntax shapes** from the repository. They are abbreviated source excerpts, not copies of complete application files. All six samples are detected. This does **not** establish recall over all 64 historic compiler positions or low false-positive rates over the full codebase.

Do not attach the strict gate to a production release workflow until an independent Windows build and full-source scanner review confirm its behavior. Keep it as a manual or non-blocking QA check first.

The remaining authoritative qualification is a Windows Release x64 build under exact recorded SDK and analyzer versions. Product Phase 1 remains **NO-GO**.
