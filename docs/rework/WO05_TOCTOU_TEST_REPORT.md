# WO-05 TOCTOU test report

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

Status: PARTIAL.

Actual Windows tests cover hardlinks, junction ancestors, parent rename, delete sharing, content writes and same-hash object swaps.
Every destination handle opens before existing content changes.
A later locked destination leaves the earlier target unchanged.
Recovery remains required while the lock prevents restore. Recovery completes after the lock closes.
Same-hash replacement targets and backups fail identity checks.
An unrelated replacement file is not deleted during rollback.
The input package cannot move or change between verification and staging.
Exclusive update.lock prevents simultaneous apply and recovery.

| Scenario | Evidence status |
|---|---|
| Junction/reparse ancestor and hardlink leaf | PASS, actual local Windows fixtures |
| Parent rename, file deletion and file writes with retained handles | PASS |
| Backup and target replacement identities | PASS |
| Locked later destination and retry | PASS |
| Names, reserved devices, case collisions and malformed paths | PASS, package validation tests |
| Simultaneous apply/recovery | PASS, exclusive lock fixture |
| Symbolic-link insertion with VM privileges | NOT TESTED |
| Live ACL mutation by another principal | BLOCKED, VM required |
| Existing writable memory mapping | NOT TESTED |
| Journal rename instant termination and power loss | NOT TESTED |
| Installed helper image self-update | NOT TESTED; lock risk remains P0 |

Journal replacement closes its temporary handle before File.Move.
The protected parent ACL excludes normal-user replacement. Administrators remain inside this trust boundary.
No claim covers hostile administrators, kernel access or all file-system race conditions.
