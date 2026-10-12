# WO-15 재생 공격 결과

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 전후 결과
기존 공개 unanchored 경로의 재생은 WO14 재시험에서도 UNMITIGATED다.
새 내부 anchored 경로는 제한된 위협 모델에서 차단을 확인했다.

| 시험 | 결과 | 증거 |
|---|---|---|
| 과거 Recovery Store / Used 이전 blob | BLOCKED | 별도 프로세스 probe 및 WO15 회귀 |
| 과거 전체 금고 | BLOCKED | 앵커 최신 상태 유지 |
| 과거 활성화 포인터 | AuthenticButStale | 기존 비밀번호 인증과 최신성 판정 분리 |
| 금고와 앵커 함께 재생 | ACCEPTED / UNSUPPORTED | 실제 공격 성공을 PARTIAL로 기록 |
| 앵커 삭제 | AnchorMissing | 자동 재등록 없음 |
| 앵커 손상 | AnchorCorrupted | 파일 보존 |
| 다른 앵커 인스턴스 | AnchorReset | 승인 차단 |
| 과거 Header·Metadata 혼합 전체 조합 | NOT TESTED | 기존 세대 인증 회귀만 존재 |

오래된 인증 상태에서도 읽기 전용 파일 바이트를 확인했다.
읽기 전용 회수는 최신 복구 코드 재활성화 권한을 만들지 않는다.
앵커와 금고 동시 재생의 공격 성공을 보안 PASS로 계산하지 않았다.
