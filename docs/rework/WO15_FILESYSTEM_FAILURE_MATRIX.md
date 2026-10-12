# WO-15 파일 시스템 실패 행렬

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 실행과 미실행 구분
| 시나리오 | 판정 | 정확한 근거와 제한 |
|---|---|---|
| Actual disk full | NOT TESTED | No bounded disposable volume provided; exception injection is not disk-full proof |
| ACL access denied | NOT TESTED | No independent SID ACL fixture executed |
| Read-only volume | NOT TESTED | No disposable read-only volume executed |
| File lock | PASS | WO14 Locked_snapshot_file_is_rejected_without_changing_source; WO15 exclusive state.lock |
| Metadata partial write | PARTIAL | Existing corruption/metadata interruption tests pass; arbitrary storage partial-write matrix not executed |
| Journal write crash | PARTIAL | Existing WO13 journal failure tests pass; no new WO15 journal mid-byte kill |
| Activation replacement crash | PASS | WO14 transition 10 kills and v6 password 10 kills preserve authenticated originals |
| Anchor write failure | PASS | AnchorBeforeReplace/AfterReplace exception and separate-process kill tests |
| Receipt write failure | PASS | Anchor replacement occurrences 1/2/3 cover reserve/commit/ack interruption |
| Symbolic-link replacement | NOT TESTED | Existing boundary rejection is not a live WO15 replacement race |
| Junction replacement | NOT TESTED | Existing boundary rejection is not a live WO15 replacement race |
| Hardlink replacement | PARTIAL | WO14 hardlinked snapshot rejection passes; live replacement race not executed |
| Original directory replacement | PARTIAL | WO14 held snapshot handles reject replacement/mutation; separate live replacement attack not executed |
| File added during transition | PASS | WO14 Concurrent_inventory_addition_is_visible_and_preserved |
| Concurrent Create | NOT TESTED | No new WO15 two-process create race |
| Import with PasswordChange | NOT TESTED | Coordinator reuse inspected; explicit mixed-operation race not executed |
| Export with Lock | NOT TESTED | Existing Lock waits for import is a different operation pair |
| Recovery with Dispose | NOT TESTED | No new WO15 explicit race |

실제 Windows NTFS 파일 시험과 별도 프로세스 종료를 수행했다.
원본 보존과 인증된 추출은 별도 확인 항목이다.
일반 예외 주입을 실제 디스크 가득 참이나 전원 차단으로 분류하지 않았다.
VaultFileBoundary의 핸들·파일 ID 검사를 재사용했다. 링크 방어를 제거하지 않았다.
신규 앵커의 전체 timestamp/state validation과 실권한 ACL 시험은 후속 P1이다.
