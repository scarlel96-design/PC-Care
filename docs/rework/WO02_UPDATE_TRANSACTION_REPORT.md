# WO-02 업데이트 트랜잭션 검토

판정: **BLOCKED**. 현재 v1 패키지는 내용 검증만 가능하고 발행자 인증이 없다. `UpdatePackageInspector`는 정상 v1 ZIP을 `ContentVerifiedOnly / PUBLISHER-UNVERIFIED / CanApply=false`로 반환한다. `UpdateInstallerService.ApplyAsync`는 호출 시 패키지를 다시 검사한 뒤 설치를 거절한다. `UpdatePendingApplier`는 기존 보류 상태를 실행하지 않으며, 이미 해당 버전이 설치된 stale 마커만 정리한다.

기존 PowerShell 스크립트는 스테이징 폴더를 재귀 열거해 복사했고 최종 적용 시 매니페스트 허용 목록과 파일 ID를 재검증하지 않았다. 이 경로를 제거했다. PowerShell 유지와 제한 명령만 받는 보조 실행 파일을 비교하면, 보조 실행 파일이 입력 스키마·해시·ACL·재분석 지점·저널 상태를 한곳에서 강제하기 쉽다. 후속 구현은 보조 실행 파일을 선택한다.

현재 보장: 미인증 패키지와 오래된 pending state로는 PC-Care가 설치 파일을 복사하거나 UAC 스크립트를 실행하지 않는다. 현재 미보장: 정상 자동 업데이트, 보호 스테이징, 파일 핸들 기반 TOCTOU 방어, 백업 무결성, 원자적 전환, 복원 저널, 정전·강제 종료 복구, 관리자 권한 E2E. 여러 파일의 순차 교체를 원자적 완료로 간주하지 않는다.

후속 상태 머신: Prepared → Verified → Applying → PostApplyVerification → Committed; 실패 시 Failed → RollbackPending → RollingBack → Restored 또는 RecoveryRequired. 각 상태는 디스크 동기화된 저널과 이전 설치 해시로 증명해야 한다. 최종 성공 전까지 이전 파일 백업을 지우지 않아야 한다.

격리 테스트 설치 디렉터리 시나리오: 정상·누락·중복·추가 파일, 변조, 파일 잠금, 접근 거부, 부분 적용, 취소, 프로세스 종료, 재시작, 롤백 실패, 스테이징 바꿔치기. 현재 실제 앱 내 적용 경로가 닫혀 있어 이 시나리오의 생산 E2E 결과는 **NOT TESTED**다.
