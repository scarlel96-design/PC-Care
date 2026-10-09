# WO-02 실행 보고서

기준 커밋: `13828584176f3f1b8ee390b43df1ff86d76c2489`  
작업 브랜치: `codex/pccare-rework-phase1-closure`  
전체 판정: **PARTIAL / Phase 1 NO-GO**.

| 작업 | 판정 | 확인된 결과 |
|---|---|---|
| 업데이트 패키지 내용 검증 | PASS | v1 ZIP의 실제 바이트·허용 목록 검증을 유지하고, 발행자 미인증 상태를 별도로 표시한다. |
| 관리자 권한 업데이트 | BLOCKED | 기존 PowerShell 전체 열거 복사 경로를 제거했다. 발행자 신뢰 루트와 트랜잭션 보조 실행 파일이 없으므로 앱 내 적용은 차단한다. |
| 업데이트 실패 복원 | BLOCKED | 생산 적용 자체를 차단해 추가 손상을 막는다. 설치 전후 상태 저널과 자동 복원은 아직 구현되지 않았다. |
| AV3 생산 게이트 | PARTIAL | AV3를 ReadOnlyValidated로 표시하고 Writer·Journal·Migration 생산 경로와 외부 witness stub의 생산 가용성을 닫았다. 기존 사용자 금고는 변환하지 않았다. |
| 런타임 신뢰 | PARTIAL | 서명 검사 생략을 UnsignedAllowed로 분리하고 WinVerifyTrust를 도입했다. 발행자 핀이 없어 Verified는 발생하지 않는다. |
| 시스템 케어 격리 | PARTIAL | 이동 전 영속 저널, 해시 확인, 충돌 시 원본 보존을 추가했다. 강제 종료·디스크 장애 전체 매트릭스는 미검증이다. |
| Skip·CI | PARTIAL | 기준 116개 전수 목록화, 핵심 생산 게이트 5개 재활성화, Windows CI 정의 추가. |
| 실제 Windows E2E | NOT TESTED | 사용 중 설치본과 실제 관리자 권한 업데이트를 실행하지 않았다. 합성 패키지 프로브 결과는 별도 증거를 따른다. |

주요 원인: v1 업데이트 매니페스트는 파일 해시와 목록 지문을 담지만 발행자 서명/키 바인딩이 없다. 이전 스크립트는 검증 목록을 최종 적용 단계에 재강제하지 않았다. AV3 생산 승인을 나타내는 상수는 실제 승인/내구성/암호 구현 증거와 일치하지 않았다.

코드 변경: `src/SmartPerformanceDoctor.App/Services/Update`, `src/SmartPerformanceDoctor.App/Services/Runtime*`, `src/SmartPerformanceDoctor.App/Services/SystemCare`, `src/SmartPerformanceDoctor.AstraVault`. 테스트와 CI 변경은 `tests/`, `.github/workflows/wo02-windows-security.yml`에 있다.

증거: `evidence/wo02/`의 검사·빌드·테스트·롤백 파일. 기준 커밋의 `UpdateInstallerService.cs` SHA-256은 `75DD62BA1A2CE07CF0E332BAC60A9D33E5FB6B26884C4E237514CBD0FB982232`이며 원본 사본과 diff를 보존했다.

최종 Release x64 앱 빌드는 경고 0개·오류 0개로 통과했다. 전체 테스트는 752개 중 641개 통과, 실패 0개, Skip 111개였다. 합성 패키지·시스템 케어·AV3 차단 프로브는 통과했으며, 설치본 UAC·재부팅·실패 복원 E2E는 수행하지 않았다.

다음 단계: 공식 발행자 공개 키/서명 정책을 확정하고 제한된 업데이트 보조 실행 파일에 허용 목록 재검증, 보호 스테이징, 이전 버전 백업·저널·재시작 복구를 구현한다. 그 뒤 격리된 Windows 설치 디렉터리에서 관리자 권한·강제 종료·롤백 E2E를 실행한다.
