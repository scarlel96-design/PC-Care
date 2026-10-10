# WO-05 file identity audit

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

Status: PARTIAL. Actual local Windows handle tests: PASS.

| Boundary | Implemented check | Retained protection |
|---|---|---|
| Source package | Signature and content on the opened stream | One read handle through staging and completion |
| Directory ancestors | Directory type, no reparse flag, final path, volume and file ID | Read-attributes handles exclude delete sharing |
| Stage files | Length, SHA-256, final path, single link | Read handles remain open through commit |
| Existing targets | Original ID, length and SHA-256 | Every destination write handle opens before content writes |
| Backups | Original content, backup ID and ACL | Read handles remain open through apply or restore |
| Restored targets | Original ID and accepted current content | Same target handle performs copy and post-verification |
| New-file deletion | Persisted installed ID and accepted content | SetFileInformationByHandle deletes the opened object |
| Committed package cleanup | Recorded package identity | Opened deletion handle validates identity first |

CreateFileW uses OPEN_REPARSE_POINT on the leaf. Ancestor pins address parent junctions separately.
Directory handles use BACKUP_SEMANTICS. FileIdInfo returns a volume value and a 128-bit identifier.
The persisted format contains 49 hexadecimal/separator characters.
GetFinalPathNameByHandleW checks the opened object's normalized path.
File identity proves object continuity within the tested lifetime. It does not authenticate an owner or prevent all privileged attacks.
File IDs can be reused after deletion. Hashes and restrictive sharing remain necessary.
Existing writes use WriteThrough and Flush(true). Rename durability under power loss remains untested.
Only local fixed disks are accepted. UNC, remote drives, links and unsupported file ID queries fail closed.
Windows case-sensitive directories, long-path boundary behavior and unusual file systems require additional measurements.

References:
- [CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew)
- [GetFileInformationByHandleEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex)
- [GetFinalPathNameByHandleW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew)

Evidence: FILE_ID_TEST_RESULT.txt and TOCTOU_TEST_RESULT.txt.
