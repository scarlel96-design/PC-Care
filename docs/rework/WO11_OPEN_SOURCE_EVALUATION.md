# WO-11 — Open-source and official API evaluation

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Selected implementation

Reuse the existing WindowsFileBoundary handle and identity implementation.
Reuse the existing RecoverySandbox transaction implementation for synthetic fixtures.
Reuse ObservableObject and DiagnosticLifecycle for WinUI integration.
Keep TraceEvent 3.2.8, CsWin32 0.3.358, and the existing Windows App SDK package configuration.
No new runtime library was added.

| Candidate | Evidence and decision |
|---|---|
| Windows storage APIs / .NET DriveInfo | Report capacity, caller-available space, and total free space separately. Allocation and quota effects remain distinct. |
| Windows file handles and identity | Existing wrapper pins ancestors, checks file identities, and rejects reparse points and hardlinks. Reuse avoids a second mutation engine. |
| Restart Manager | RmRegisterResources writes registration information. RmGetList identifies registered users, but does not prove future availability. Do not use shutdown or restart operations. |
| CommunityToolkit.Mvvm 8.4.2 | MIT. Maintained upstream. Existing ObservableObject meets this bounded screen's needs. No package adoption or runtime compatibility claim. |
| Windows App SDK / WinUI 3 | Reuse existing controls, theme tokens, and UI Automation properties. Current SDK 2.5.1 differs from the baseline configuration. No upgrade was performed. |
| Existing CareFolderScanner | Retain legacy scan reporting. Its path-based findings do not authorize the new cleanup executor. |

## Security and compatibility evidence

The local NuGet vulnerability audit returned exit 0 and no listed vulnerable packages.
This result depends on the queried feeds. It does not prove the absence of vulnerabilities.
The Release solution build returned zero warnings and zero errors.
The test-only CleanupProbe builds separately and has no production dependency edge.
No sensor driver, elevated service, or rule execution package was installed.
Long-duration stability and low-end hardware measurements remain NOT TESTED.

## Primary sources

- [GetDiskFreeSpaceEx](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getdiskfreespaceexw): caller availability and total free space differ.
- [FILE_STANDARD_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_standard_info): allocation size differs from logical file length.
- [GetFileInformationByHandleEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex): handle-based file metadata.
- [Hardlinks and junctions](https://learn.microsoft.com/en-ca/windows/win32/fileio/hard-links-and-junctions): multiple names can identify one file.
- [RmRegisterResources](https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmregisterresources): registration costs and effects.
- [RmGetList](https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmgetlist): registered resource users.
- [CommunityToolkit releases](https://github.com/CommunityToolkit/dotnet/releases) and [NuGet package](https://www.nuget.org/packages/CommunityToolkit.Mvvm).
- [Windows App SDK downloads](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads).
