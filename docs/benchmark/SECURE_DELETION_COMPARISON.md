# WO28 - Secure deletion comparison

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

## Microsoft SDelete

The official page describes file/directory deletion and free-space cleaning. Its NTFS explanation distinguishes allocated file data, compressed/sparse allocation and MFT handling. Free-space cleaning does not erase old directory filenames. These are documented behavior and limitations, not measurements from this work. [SDelete](https://learn.microsoft.com/en-us/sysinternals/downloads/sdelete).

The reviewed Sysinternals terms restrict decompilation/disassembly except where applicable law permits it. No independent right for binary analysis was established. SDelete was neither downloaded for analysis nor decompiled here. [Terms](https://learn.microsoft.com/en-us/sysinternals/license-terms).

## Media limitations

VeraCrypt documents that wear-leveling can retain earlier sector versions. Its warning reinforces the need to distinguish logical overwriting from physical sanitization. It does not certify PC-Care or SDelete on any device. [Wear-leveling](https://veracrypt.io/en/Wear-Leveling.html).

## PC-Care assessment

| Concern | Actual code evidence | Classification |
|---|---|---|
| Media limitation text | `SecureDeleteStorageProfiler` explicitly limits SSD/NVMe file Clear claims | 부분 구현 |
| Unsupported files | `SecureDeletePreflight` checks reparse points, links, special attributes, locks and bounded enumeration | 부분 구현 |
| Exact target and audit | Final-handle path, `SecureDeleteAuditChain`, durable journal and read-only reconciliation exist | 부분 구현 |
| Cancellation result | `SecureDeleteExecutionResult` separates cancellation, mutation, incomplete targets and audit verification | 부분 구현 |
| Folder execution | Production gate remains blocked | 개선 필요 |
| Physical Purge/Sanitize | No device qualification was performed here | 추가 근거 필요 |
| Adopting SDelete | A different destructive engine would not close current evidence gaps | 도입 가치 없음 |

## Product work

Keep file Clear, free-space wipe and device sanitization separate. State supported allocation types and unresolved copies before approval. Show mutation and audit completeness independently after interruption. Do not offer original-content restoration after destructive writes.

Prioritize read-only explanation and existing journal recovery tests. Qualify each supported physical device separately before introducing a device-level action. No destructive competitor tool was executed.
