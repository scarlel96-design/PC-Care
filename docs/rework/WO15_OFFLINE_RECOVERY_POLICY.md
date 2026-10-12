# WO-15 오프라인 복구 정책

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 구현된 처리
DPAPI 앵커는 인터넷을 사용하지 않는다. 콘텐츠 인증은 오프라인에서도 유지한다.
앵커가 누락되면 내부 trusted recovery를 차단한다. 복구 중 자동 앵커 등록은 수행하지 않는다.
현재 비밀번호로 인증된 읽기 전용 접근은 별도 경로다.
이 경로는 과거 상태를 최신으로 승격하지 않는다.
복구 코드를 사용한 후 결과가 모호하면 OperationId로 영수증을 조회한다.
Reserved는 Interrupted로 표시한다. 소비를 자동 취소하지 않는다.

## 후속 운영 절차
1. Preserve the original vault and anchor files.
2. Authenticate the readable generation without enabling writes.
3. Export verified bytes into an approved protected destination.
4. Compare all exported hashes with the authenticated metadata.
5. Review enrollment and recovery-code revocation before creating a new anchor.

이 절차의 관리자 승인 UI와 새 앵커 이관 도구는 구현되지 않았다.
TPM 장애, 외부 Witness 장애, Windows 재설치와 장치 교체는 NOT TESTED다.
정상 백업 복원용 코드 폐기·재발급의 전체 운영 흐름은 PARTIAL이다.
평문 추출 결과는 사용자가 보호해야 한다. 이번 시험은 합성 출력만 사용했다.
