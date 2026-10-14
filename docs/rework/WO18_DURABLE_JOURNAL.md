# WO-18 — DURABLE_JOURNAL

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 저장 계약
각 레코드는 Sequence, PreviousMac, OperationId와 StoreIdentity를 포함합니다.
HMAC-SHA256은 레코드 순서와 내용을 인증합니다.
기존 DPAPI CurrentUser 키 저장소를 재사용합니다.
대상 경로는 OperationId 결합 DPAPI로 보호합니다.
콘텐츠 SHA-256은 HMAC 지문으로 기록합니다. 평문 콘텐츠는 저장하지 않습니다.
소유권은 HMAC 지문으로 기록합니다.
새 저장 디렉터리는 현재 SID와 SYSTEM에 상속 FullControl을 부여합니다.
기존 디렉터리는 소유자와 쓰기 권한을 검사합니다.
파일은 WindowsFileBoundary의 동일 핸들에서 기록하고 Flush(true)를 호출합니다.
빈 파일, 잘린 마지막 레코드, 잘못된 JSON 및 인증 불일치를 거부합니다.
읽기 예산은 16MiB, 10,000개 레코드입니다. 쓰기 레코드는 64KiB 이내입니다.
결과 JSON은 4MiB 이내에서 동일 핸들로 읽고 봉인 해시와 비교합니다.

## 신뢰 범위
같은 SID의 공격자는 DPAPI 키에 접근할 수 있습니다.
관리자와 전체 저장소 재생에 대한 독립 최신성 보장은 없습니다.
인증된 과거 접두 레코드 재생도 독립 앵커 없이 완전히 탐지하지 못합니다.
이 경우에도 조정은 읽기 전용이며 삭제 권한을 부여하지 않습니다.
실제 저장 공간 부족, 장치 Flush 오류 및 물리 전원 차단은 NOT TESTED입니다.
주입된 기록 실패는 실제 장치 오류와 구분합니다.

## 공식 API 확인
[FileStream.Flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)는 중간 버퍼 Flush를 제공합니다.
[FlushFileBuffers](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers)는 파일 핸들의 버퍼를 기록합니다.
이 API 호출을 물리 전원 차단 내구성의 실측 증거로 사용하지 않습니다.
[DPAPI](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata)의 사용자 보호 범위를 독립 Witness로 표현하지 않습니다.
새 암호화 알고리즘과 외부 패키지를 추가하지 않았습니다.

## 최종 검증 증거

CI 검증 소스는 `d3467ea91ad916ff405705939f0c62c65e70153f`입니다. 소스 트리는 `70902843929bfe50e16a2892ccb2f7b0932e8c82`입니다.
[Windows CI 38537248047](https://github.com/scarlel96-design/PC-Care/actions/runs/38537248047)는 SUCCESS입니다.
앱 전체는 1,214 PASS / 0 FAIL / 111 SKIP입니다. 신규 WO-18은 36 PASS입니다.
업데이트 128개와 성능 233개는 앱 전체 집계에 포함합니다. SecurityLab은 별도 36 PASS입니다.
Restore와 Release의 경고 및 오류는 각각 0입니다.
삭제의 별도 프로세스 중단 17건은 읽기 전용 조정과 다른 파일 보존을 확인했습니다.
기존 합성 격리 중단 6건은 원본 SHA-256과 복원 SHA-256이 일치했습니다.
CI 스모크와 정적 UI 시험은 실제 GUI 검증이 아닙니다. 실제 GUI는 NOT TESTED입니다.
독립 SID, 저사양 장비, 물리 전원 차단과 실제 저장장치 Purge는 미검증입니다.
전체 판정은 PARTIAL입니다. Phase 1 공식 출시 판정은 NO-GO입니다.
