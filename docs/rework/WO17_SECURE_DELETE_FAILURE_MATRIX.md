# WO-17 — 취소와 부분 실패

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

결과 상태는 Completed, PartiallyCompleted, Cancelled, Failed, RecoveryReviewRequired입니다.
항목 상태는 TargetChanged, Unauthorized, ValidationFailed를 추가로 구분합니다.
취소 예외를 별도로 처리하고 후속 대상 처리를 중단합니다.
변경 가능성이 있는 취소·실패 결과에는 PartialWritePossible을 기록합니다.
취소 전 덮어쓴 바이트는 원복되지 않습니다. 보안 삭제는 격리와 다릅니다.
파일별 삭제 성공과 전체 작업 성공을 구분합니다.
진행률 100은 전체 완료에서만 표시합니다.
감사 기록 실패는 호출 실패로 드러납니다. 삭제된 바이트 복원을 약속하지 않습니다.
실제 시험: 변경된 내용, 동시 쓰기, 경로 이름 교체, 취소 전·후, ADS, 읽기 전용, 중복 작업.
프로세스 종료 후 보안 삭제 중단 조정과 디스크 부족 시험은 NOT TESTED입니다.
격리 조치의 별도 프로세스 복구는 `CRASH_RECOVERY_RESULT.json`에 별도로 기록합니다.
