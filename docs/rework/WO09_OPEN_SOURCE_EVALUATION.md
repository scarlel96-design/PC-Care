# WO-09 technology evaluation

Status: PARTIAL.

Branch: `codex/pccare-diagnostic-qualification-recovery-planning`.
Baseline: `5ac4c4d5a4420bba9b9a1d2bbd8d3de12047c96a`.
Validated source: `888ec1e8de1fe4fc2bcb7b2217e544d20dc929e7`.
Phase 1: NO-GO.

## Selected technologies

| Technology | Version | License | Use and verification |
|---|---|---|---|
| TraceEvent / PerfView | 3.2.8 retained | MIT | Existing bounded ETW collector. Preserve the tested version. |
| CsWin32 | 0.3.358 retained | MIT | Existing generated PDH and snapshot bindings. |
| Microsoft TraceProcessing All | 1.12.10 | Microsoft SDK terms; acceptance required | Independent offline ReferenceProbe. .NET 10 Release compilation passed. |
| WPR | Windows component | Windows/WPT terms | Private file-mode ETL capture on isolated CI. |
| WPA Exporter | Evaluated; not installed locally | Windows/WPT terms | Requires installed exporter and export profiles. |
| Microsoft RulesEngine | Evaluated; not adopted | MIT | Dynamic expressions add unnecessary execution surface for eight review actions. |

TraceProcessing uses an independent Microsoft parser rather than the PC-Care TraceEvent parser.
It provides CPU samples, process lifetimes, disk activity, metadata, and loss counters.
Its latest listed stable All package is 1.12.10, dated 2023-08-23.
That date does not prove active recent maintenance.
The All package is an aggregation package with native toolkit dependencies.
Its license is not MIT and it is not classified as open source here.
The SDK remains in a separate test tool. The application does not deploy it.
NuGet vulnerability audit returned no known vulnerable packages for ReferenceProbe on 2026-10-11.
This audit does not establish native toolkit security or production redistribution qualification.
The license URL resolved to Microsoft's RTF agreement with HTTP 200.

Cancellation is bounded by runner deadlines and private-process lifetime.
Offline parsing has a 1-GiB ETL limit. Peak memory is measured separately.
CPU, memory, and runtime support require actual Windows execution evidence.
No sensor driver was installed. Temperature and clock remain Unknown.
The planner uses a compiled action enum and existing Finding contracts.
It loads no external expressions or executable rules.

## Primary sources

- [TraceProcessing quickstart](https://learn.microsoft.com/en-us/windows/apps/trace-processing/quickstart)
- [Package and release metadata](https://www.nuget.org/packages/Microsoft.Windows.EventTracing.Processing.All/1.12.10)
- [Microsoft SDK license](https://aka.ms/TraceProcessingLicense)
- [WPA Exporter](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/exporter)
- [WPR providers and buffers](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/2-system-and-event-provider-definitions)
- [TraceEvent source](https://github.com/microsoft/perfview)
- [CsWin32 source](https://github.com/microsoft/CsWin32)
- [RulesEngine source](https://github.com/microsoft/RulesEngine)


## Evidence and restoration

Read `evidence/wo09/VERIFICATION.txt` for commands, inputs, outputs, and source-copy restoration.
Read `evidence/wo09/GITHUB_RESULT.txt` for the publication commit and CI source.
Use a reviewed Git revert to restore repository changes.
The source-copy rollback does not test an installation transaction.

## Runtime and integration boundaries

| Technology | Permission and resource boundary | Compatibility evidence | Integration cost |
|---|---|---|---|
| TraceEvent | Kernel sessions require elevation. Existing bounded queues and cancellation remain active. | Existing .NET 10 Windows CI passes. | Reuse the existing collector without upgrading its package. |
| CsWin32 | Generated PDH queries run under the current token. | Existing .NET 10 Release build passes. | Reuse generated bindings; add no custom counter ABI. |
| TraceProcessing | Offline parsing reads private ETL. Capture requires the isolated elevated runner. | x64 .NET 10 build and hosted Windows parsing run separately. | One test-only console project and normalized comparison contracts. |
| WPR/WPA | Kernel capture requires elevation. ETL can contain private paths and process metadata. | WPR runs on the hosted Windows image. WPA Exporter is NOT TESTED. | A bounded file-mode profile and private lifecycle harness. |
| RulesEngine | Dynamic rule evaluation requires additional rule trust and expression validation. | No integration experiment was performed. | The current eight review labels use compiled policies instead. |

Windows 10 and 11 coverage is incomplete. Only recorded local and runner OS builds are verified.
TraceProcessing cancellation uses the containing job deadline. In-process cooperative parser cancellation is not implemented.
Long-duration collector and offline-parser stability remain NOT TESTED.
No recent package publication or audit proves absence of vulnerabilities.
Independent parser resource measurements appear in each CI_REFERENCE report.
