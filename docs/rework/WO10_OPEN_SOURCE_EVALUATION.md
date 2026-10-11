# WO-10 open source evaluation

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Selected technology

| Technology | Version | License | Decision and integration |
|---|---|---|---|
| Microsoft TraceEvent / PerfView | 3.2.8 | MIT | Retain the existing ETW collector. |
| Microsoft CsWin32 | 0.3.358 | MIT | Reuse generated PDH and Windows snapshot APIs. |
| Microsoft TraceProcessing | 1.12.10 | Microsoft SDK license | Reuse CPU sampling, scheduling, process, and disk analysis. |
| Microsoft UI Automation | Installed Windows / .NET 10 | Windows platform terms | Build a small external read-only GUI harness. |
| FlaUI | 5.0.0 reviewed | MIT | Defer adoption until live WinUI compatibility can be tested. |

TraceProcessing is an official SDK. This report does not label it as MIT open source.
Existing package versions remain pinned. No package upgrade occurred.
The ReferenceProbe and GuiProbe compile on .NET 10 x64.
Windows 10 live compatibility remains NOT TESTED. The local host and CI use Windows 11-family builds.
NuGet audit returned no reported vulnerable dependency for Diagnostics or ReferenceProbe.
This result does not prove that all dependencies are vulnerability-free.
No sensor driver or administrator service was installed.
TraceProcessing remains an offline test tool. Its memory cost is separate from the product collector.

## Official sources

- [TraceEvent package](https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent/3.2.8)
- [CsWin32 repository](https://github.com/microsoft/CsWin32)
- [TraceProcessing package](https://www.nuget.org/packages/Microsoft.Windows.EventTracing.Processing.All/1.12.10)
- [TraceProcessing tutorial](https://learn.microsoft.com/en-us/windows/apps/trace-processing/tutorial)
- [CPU sample weight](https://learn.microsoft.com/en-us/dotnet/api/microsoft.windows.eventtracing.cpu.icpusample.weight?view=trace-processor-dotnet-1.0)
- [Windows CPU analysis](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/cpu-analysis)
- [FlaUI releases](https://github.com/FlaUI/FlaUI/releases)
- [UI Automation overview](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-overview)

## Minimum empirical checks

ReferenceProbe Release build: PASS, zero warnings and errors.
GuiProbe Release build: PASS, zero warnings and errors.
Read-only collector: PASS for CPU and memory; PDH CPU Unknown; ETW permission denied locally.
Live UI Automation: NOT TESTED. Offline SDK analysis runs separately on the isolated Windows CI runner.
See OPEN_SOURCE_EVALUATION.txt for commands and audit results.
