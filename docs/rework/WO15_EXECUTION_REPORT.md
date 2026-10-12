# WO-15 최종 실행 보고서

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 실제 구현
DPAPI CurrentUser 시험 앵커, freshness 계약, HMAC 복구 코드 식별자, 예약·확정·확인 영수증을 구현했다.
앵커 실패 시 생산 승격을 차단했다. 세션 확보 전에 코드가 소모되는 경로를 내부 시험 호출에서 피했다.
공개 v4/v5 비밀번호 변경의 위험한 다중 파일 갱신을 차단했다.
별도 프로세스 종료·재열기·바이트 대조와 추출 전용 메모리 측정을 추가했다.
독립 SID GUI 실행 준비 스크립트를 추가했다. 실제 GUI 실행 결과는 없다.
중복 NuGet 참조를 제거했다. 기존 보안 회귀와 생산 승인 값을 보존했다.

## 정량 결과
앱 1,108 PASS / 0 FAIL / 111 SKIP / 1,219 total. WO15 추가 23개다.
SecurityLab 36 PASS / 0 FAIL / 0 SKIP. 성능 233과 업데이트 128은 앱 집계에 포함된다.
WO14 사전 회귀 30 PASS. 전환 10개 중단과 v6 비밀번호 10개 중단을 재확인했다.
복구 영수증 시험 15개 중단에서 기존 바이트를 확인했다. 모호한 코드 소비 2개 구간은 검토 상태다.
원래 v5 비밀번호 중단 6개 중 4개는 인증된 바이트를 열었다. 2개는 PreservedButLocked다.
Restore 0 warnings / 0 errors. Release 0 warnings / 0 errors. 명령 exit 0.

## 추출 측정의 해석
0바이트·4KiB·4MiB·64MiB·256MiB를 별도 추출 프로세스에서 측정했다.
모든 원본과 출력 SHA-256이 일치했다. 이 프로세스에는 Import가 없다.
256MiB 추출은 1,802.3754 ms, 누적 관리 할당 15,913,808 bytes를 기록했다.
추출 구간 최대 표본 Working Set은 113,856,512 bytes다. 이전 Unlock의 메모리 잔존을 포함한다.
Unlock·Export·Hashing 수치를 각각 보존했다. Export에는 인증과 출력 비용이 함께 포함된다.
5 ms 표본은 순간 최대치를 놓칠 수 있다. 저사양 실물과 취소 반응 시간은 NOT TESTED다.
성능 개선률은 계산하지 않았다.

## 형식과 게이트
spd-vault 포맷과 보안 삭제·업데이트 엔진은 변경하지 않았다.
SecurityLab 기본 제품 형식은 v5다. v6와 새 anchor는 내부 합성 시험 전용이다.
ProductionWriterEnabled, JournalWriterEnabled, MigrationEnabled, WriterEnableReady,
ExternalReviewCompleted, ProductionEnableAuthorized는 모두 false다.
시험 성공을 AV3 생산 승인으로 해석하지 않는다.

## 변경 파일
- `.github/workflows/wo15-windows-validation.yml`
- `scripts/wo15-gui-validation.ps1`
- `src/SmartPerformanceDoctor.App/SmartPerformanceDoctor.App.csproj`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabTrustedState.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.Phase2.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.TrustedRecovery.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.cs`
- `tests/SmartPerformanceDoctor.SecurityLab.Tests/VaultV4IntegrationTests.cs`
- `tests/SmartPerformanceDoctor.Tests/WO12VaultReliabilityTests.cs`
- `tests/SmartPerformanceDoctor.Tests/WO15TrustedRecoveryTests.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/Program.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/TrustedRecoveryProbe.cs`

## 남은 P0
1. 생산 경로에 신뢰 앵커가 연결되지 않았다. 기존 공개 recovery 경로는 전체 재생에 취약하다.
2. 앵커까지 재생하는 동일 계정·관리자 공격을 방어하지 못한다.
3. 코드 소비와 앵커 확정 사이의 중단에는 검토 절차가 필요하다.
4. 이미 손상된 v5 비밀번호 상태의 인증된 복원을 보장하지 못한다.
5. 실제 UAC·재부팅·설치 ACL·공식 서명·구형 최초 bootstrap 전환 검증이 남아 있다.
6. 보안 금고 생산 승인과 보안 삭제의 실제 매체 검증이 남아 있다.

## 남은 P1
정상 세대 변경의 앵커 갱신, 영수증 전체 상태 검증, 독립 SID ACL과 18개 실패 행렬의 미실행 항목이 남아 있다.
실제 GUI·저사양 장비·물리 전원 차단·실제 구형 리더·전체 재암호화는 미검증 또는 미구현이다.
외부 암호학적 보안 리뷰와 상용 인증은 완료되지 않았다.

## GitHub와 복원
Windows CI: [38255952676](https://github.com/scarlel96-design/PC-Care/actions/runs/38255952676).
검증 소스 SHA와 문서 업로드 커밋을 구분한다. 최종 원격 확인은 GITHUB_RESULT.txt에 기록한다.
소스 복원 스크립트는 독립 MODIFIED_FILE.copy에만 작동한다.
실제 금고 데이터 복구는 probe의 별도 프로세스 추출 결과다. 두 증거를 혼동하지 않는다.
MODIFIED_FILE·DIFF_FILE·ORIGINAL_FILE·ROLLBACK.sh와 전체 변경 소스 해시를 함께 보존한다.

## GPT에 전달할 재검토 지시
```text
WO15_EXECUTION_REPORT.md와 evidence/wo15/를 검토하라.
CI 소스 SHA와 최종 원격 문서 커밋을 구분하라.
제한된 vault-only 재생 차단을 동일 SID/관리자 재생 차단으로 확대하지 말라.
15개 바이트 보존 성공과 코드 소비 exactly-once 미완성을 구분하라.
v5 두 PreservedButLocked 결과와 생산 경로 미연결을 우선 검토하라.
다음 작업은 생산 게이트를 유지한 채 명시된 P0부터 구현하라.
```

## 최종 Windows CI 증거

CI 38255952676은 SUCCESS다. 검증 소스는 `ab2c19f3027a5450a55042915af69220f7ec5ccc`다.
실행 환경은 Windows Server 2025 10.0.26100 Datacenter다. Windows 11 전용 VM 시험으로 집계하지 않는다.
앱 1,108 PASS / 0 FAIL / 111 SKIP, SecurityLab 36 PASS를 실제 TRX와 로그에서 확인했다.
WO15 집중 회귀 23 PASS, 업데이트 128 PASS, 성능 233 PASS다. 모두 앱 집계에 포함된다.
Restore와 Release는 성공했다. Release 경고와 오류는 각각 0개다.
Actions의 Node deprecation 경고는 별도로 발생했다. 컴파일 경고와 구분한다.
CI에서도 복구 영수증 중단 15개, v5 비밀번호 중단 6개와 추출 전용 프로세스 5개를 실행했다.
공동 앵커 재생은 여전히 지원 범위 밖이다. 시험 도구 exit 0을 전체 보안 목표 PASS로 해석하지 않는다.
원격 로그와 artifact를 내려받았다. 명령, 출력과 종료 코드는 `evidence/wo15/CI_RESULT.json`에 기록했다.
문서 게시 커밋은 검증 소스 이후에 생성한다. 최종 HEAD와 트리는 별도 최종 전달 보고서에서 확인한다.
