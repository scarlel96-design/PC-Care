# WO-05 출시 판정

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

최종 판정: NO-GO. 전체 상태: PARTIAL.

| 항목 | 상태 | 필요한 후속 증거 |
|---|---|---|
| 실행 중인 도우미·DLL 자체 업데이트 | PARTIAL / P0 | 실행 이미지 잠금과 쓰기 핸들 요구가 충돌할 위험을 코드 검토에서 확인했다. 신뢰된 별도 실행 위치 설계와 설치본 E2E가 필요하다. |
| 실제 보호 ACL | BLOCKED / P0 | VM에서 소유자·DACL·하위 파일·Users 거부·SYSTEM 접근·재부팅 후 유지 여부를 측정한다. |
| 관리자 공격·ACL 변경·기존 쓰기 메모리 매핑 | NOT TESTED / P0 | 보호 ACL을 수정할 수 있는 주체는 현재 신뢰 경계 내부다. 파일 ID는 암호학적 인증 수단이 아니다. |
| 저널 원자 교체와 전원 손실 | PARTIAL / P0 | 임시 파일 핸들을 닫은 뒤 File.Move를 실행한다. 보호 부모 ACL에 의존한다. 교체 순간 강제 종료와 전원 손실은 추가 시험이 필요하다. |
| 파일 생성 후 ID 저널 기록 전 중단 | PARTIAL / P0 | 빈 새 파일이 남을 수 있다. ID가 없는 파일은 복구에서 삭제하지 않는다. 실패 증거를 보존한다. |
| WO04 구형 저널 | PARTIAL / P0 | 식별자가 없는 구형 저널은 완료 상태도 거부한다. 검토된 이관·보관 절차가 필요하다. |
| 실제 설치·UAC·Windows 재부팅 | BLOCKED / P0 | 폐기 가능한 VM, 스냅샷, 승인·거부·재부팅 로그가 필요하다. |
| 공식 발행자·생산 패키지 | BLOCKED / P0 | 승인된 신뢰 루트·개인 키 운영·회전 정책·생산 서명 패키지가 필요하다. |
| 런타임 Authenticode 읽기 경계 | PARTIAL / P0 | WinVerifyTrust와 인증서 추출은 경로를 각각 읽는다. 동일 파일 핸들 연결과 실제 설치 ACL 검증이 필요하다. |
| AV3 데이터 회수·구형 실제 설치본 | NOT TESTED | 회수 도구는 Lab v4/v5만 지원한다. AV3 Writer와 마이그레이션 게이트를 유지한다. |

자동 검사와 합성 프로세스 복원은 출시 차단 항목을 대체하지 않는다.
공식 신뢰 루트와 Release 보안 게이트는 유지한다.
실제 설치본의 핵심 기능 활성화는 승인된 서명 검증 후 별도로 확인한다.
main 병합과 공식 릴리스는 수행하지 않는다.

다음 제품 개발은 별도 브랜치에서 시작할 수 있다.
ETW 진단, 원인별 병목 분석, 조치 전후 측정, 취소·복원을 우선한다.
제품 개발 진행과 출시 승인은 별도로 관리한다.
이번 작업은 전체 UI/UX 개편을 포함하지 않는다.

## 원격 Windows CI 증거

검증 소스: `0389bd3a79f9bcb08981cabb8514055b76240376`.

[Windows CI 38049619408](https://github.com/scarlel96-design/PC-Care/actions/runs/38049619408): SUCCESS.

- wo05-signed-updater.trx: TOTAL=91 PASS=91 FAIL=0 SKIP=0
- wo05-full.trx: TOTAL=843 PASS=732 FAIL=0 SKIP=111
- wo05-securitylab.trx: TOTAL=36 PASS=36 FAIL=0 SKIP=0

최종 증거 커밋은 문서만 변경한다. 최종 HEAD는 로컬 영수증과 완료 보고에서 기록한다.
원격 실행 정보와 TRX 해시는 `evidence/wo05/GITHUB_RESULT.txt` 및 `CI_TRX_MANIFEST.json`에 기록한다.
