# WO-15 TrustedStateProvider 구현

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 호출 경계
`LabTrustedState.cs`의 모든 새 구성 요소는 internal이다.
`LabVaultService.TrustedRecovery.cs`는 내부 v6 합성 시험 경로만 제공한다.
시험 앵커 생성에는 wo15-vault-fixture-* 조상과 정확한 fixture.marker가 필요하다.
이 표식은 시험 범위 제한이다. 공격자에 대한 인증 자격이 아니다.
공개 제품 UI에는 새 앵커를 연결하지 않았다. Descriptor.ProductionApproved는 false다.

FreshnessEvidence는 VaultId, 세대, 인증 세대 결합 값과 복구 저장소 결합 값을 갖는다.
앵커는 state.dpapi에 기록한다. state.lock은 변경과 조회를 직렬화한다.
파일을 고정된 핸들로 읽고 ID를 검증한다. 1 MiB와 영수증 1,000개 예산을 적용한다.
WriteThrough·Flush·재읽기 검사 후 개별 파일을 교체한다.
기존 대상 핸들을 닫은 뒤 File.Replace를 호출하는 구간은 남아 있다.
같은 사용자 공격자의 교체 경쟁을 완전히 제거했다고 판단하지 않는다.

| 상태 | 실제 처리 |
|---|---|
| FreshnessVerified | 현재 결합 값 일치 |
| AuthenticButStale | 결합 값 불일치; 검토 필요 |
| AnchorMissing | 파일 삭제 또는 미등록 |
| AnchorCorrupted | DPAPI/JSON/필드 검증 실패 |
| AnchorReset | 인스턴스 식별 불일치 |
| AnchorUnavailable | 잠금 또는 접근 오류 |
| RecoveryReviewRequired | 후속 정책 표현; 모든 경로에 연결되지 않음 |
| UnsupportedProvider | 계약 표현; TPM/Witness 구현 없음 |

FreshnessPolicy는 계약만 존재한다. 생산 정책 실행기로 연결되지 않았다.
정상 Import와 PasswordChange는 앵커를 자동 갱신하지 않는다.
영수증의 전체 타임스탬프·상태 일관성 검사는 아직 미완성이다.
생산 서비스 권한, 독립 신뢰 루트, 이전과 재등록 정책은 후속 작업이다.
