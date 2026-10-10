# WO-06 구현 및 검증 보고

작업 번호: WO-06
작업 브랜치: `codex/pccare-rework-phase1-bootstrap-recovery`
기준 커밋: `291f84daf1e950ba9cb74af0c8fea8879a11174e`
기준 트리: `33d084c1253a5b042c158600086db263f09faf89`
검증 소스 커밋: `5647b352cf41082def74bef2701b6fe707c51cc0`
검증 소스 트리: `e2e5452f31d8550050d7560899f409486e10dbb1`
최종 증거 커밋: 검증 소스 이후의 docs 전용 커밋. 실제 HEAD는 최종 Git 영수증을 참조한다.
실행 경로: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`
실행 환경: Windows 11 Pro 10.0.26200, x64, NTFS Healthy, .NET SDK 10.0.401, runtime 10.0.12.
로컬 토큰: Administrator=False. 전용 VM과 스냅샷은 미제공이다.
CI: [38054469100](https://github.com/scarlel96-design/PC-Care/actions/runs/38054469100). 정확한 결과는 `evidence/wo06/GITHUB_RESULT.txt`를 참조한다.

Status: PARTIAL. Phase 1 출시 NO-GO.

## 실제 변경 결과

보호된 별도 실행 부트스트랩과 설치 ID 바인딩을 추가했다.
앱의 업데이트 실행과 복구 시작을 새 부트스트랩에 연결했다.
Schema 1 완료 기록의 읽기 전용 영수증 이관을 추가했다.
Schema 1 미완료 기록은 원본을 보존하며 차단한다.
저널의 이전 세대와 문법 손상 복구를 추가했다.
신규 파일은 영속화된 고유 임시 경로에서 생성한다.
Authenticode 검증은 유지한 파일 핸들과 같은 provider state를 사용한다.
VM 전용 하네스와 프로파일 분리 CI를 확장했다.

## 검증 집계

| 시험 | 기준 WO-05 | 최종 로컬 WO-06 | 증감 |
|---|---|---|---|
| 업데이트 및 파일 경계 | 91 PASS | 128 PASS / 0 FAIL / 0 SKIP | +37 |
| 앱 전체 | 732 PASS / 111 SKIP | 769 PASS / 0 FAIL / 111 SKIP / 880 TOTAL | +37 PASS |
| SecurityLab | 36 PASS | 36 PASS / 0 FAIL / 0 SKIP | 0 |
| Release 빌드 | 경고 0 / 오류 0 | 경고 0 / 오류 0 | 유지 |
| 합성 Windows 프로세스 | 별도 종료 및 복구 | 121개 명령, 예상 결과 일치 | 확장 |

업데이트 128개는 앱 전체 880개에 포함된다. 독립 합계로 더하지 않는다.
Skip 111개는 유지했다. Skip을 통과로 집계하지 않았다.
최종 원격 CI 집계는 GITHUB_RESULT.txt와 다운로드한 TRX에 기록한다.

## GitHub에서 소스만 보고 알 수 없는 실행 정보

로컬 시험은 Windows 11 Pro 10.0.26200과 NTFS C:에서 실행했다.
시험 명령은 일반 사용자 토큰으로 실행했다.
ProgramData 보호 영역과 실제 설치본을 대상으로 쓰기 시험을 하지 않았다.
합성 프로세스의 ACL 엄격 모드는 꺼져 있다.
이 결과는 실제 관리자 경계의 ACL 검증을 대체하지 않는다.
실제 서명 시험 입력은 로컬 Microsoft dotnet.exe의 복사본이었다.
PC-Care 공식 서명 파일은 제공되지 않았다.
대화형 CLI는 실제 PTY에서 비밀번호를 에코 없이 입력했다.
입력한 값은 공개된 합성 시험 비밀번호였다.
실제 사용자 비밀번호, 금고와 평문 자료는 사용하지 않았다.
Lab v5 원본 전체 파일 해시를 회수 전후 비교했다.
내보낸 파일 SHA-256은 11630EF3F23673269819F72EF686E436FB55802B1A2F4E9E5A8A20D016A9A7ED였다.

소스 복원은 artifacts/wo06-source-rollback의 표시된 독립 파일에 실행했다.
원본 해시는 63A4AD13AB5A4B105C4B438DD4DC25E3F1449AE918EBA005946103DC77871C94였다.
수정본 해시는 0E04527306FFE4A0D8512E11BD4B2EB889A09B8002996BC8E26903E884B5D79F였다.
수정된 작업 소스와 MODIFIED_FILE은 수정 상태를 유지한다.
ROLLBACK.sh와 PowerShell 복원 모두 종료 코드 0을 확인했다.

초기 시험 실패도 기록했다.
기준 시험의 KillWorker assets 누락은 restore로 수정했다.
초기 시험 프로세스 연결 제한은 최종 실행에서 해소됐다.
이름 변경 버퍼 오류는 수정한 뒤 전체 회귀를 재실행했다.
실행 중인 시험 도우미 때문에 probe 재빌드가 잠겼다.
해당 합성 프로세스만 종료한 뒤 빌드가 통과했다.
솔루션 출력과 x64 직접 테스트 출력의 경로가 달랐다.
최종 직접 빌드로 128개 테스트가 있는 어셈블리를 확인했다.
이전 119개 또는 전체 760개 결과를 최종 결과로 사용하지 않았다.

## 실제 변경 파일

- `.github/workflows/wo06-windows-validation.yml`
- `scripts/wo06-process-validation.ps1`
- `scripts/wo06-vm-harness.ps1`
- `src/SmartPerformanceDoctor.App/Services/RuntimeAuthenticodeVerifier.cs`
- `src/SmartPerformanceDoctor.App/Services/Update/UpdateInstallerService.cs`
- `src/SmartPerformanceDoctor.App/Services/Update/UpdateRecoveryCoordinator.cs`
- `src/SmartPerformanceDoctor.UpdateEngine/AuthenticodeBoundary.cs`
- `src/SmartPerformanceDoctor.UpdateEngine/BuildPublisherTrust.cs`
- `src/SmartPerformanceDoctor.UpdateEngine/UpdateBootstrap.cs`
- `src/SmartPerformanceDoctor.UpdateEngine/UpdateTransaction.cs`
- `src/SmartPerformanceDoctor.UpdateEngine/WindowsFileBoundary.cs`
- `src/SmartPerformanceDoctor.UpdateHelper/Program.cs`
- `tests/SmartPerformanceDoctor.Tests/WO06AuthenticodeTests.cs`
- `tests/SmartPerformanceDoctor.Tests/WO06RecoveryTests.cs`
- `tests/SmartPerformanceDoctor.Tests/WindowsFileBoundaryTests.cs`
- `tests/SmartPerformanceDoctor.UpdateRecoveryProbe/Program.cs`
- `tools/SmartPerformanceDoctor.VMTestKit/Program.cs`
- `tools/SmartPerformanceDoctor.VMTestKit/SmartPerformanceDoctor.VMTestKit.csproj`

문서와 증거 파일은 검증 소스 이후 docs 전용 커밋으로 추가한다.
DIFF_FILE.patch는 기준 커밋부터 검증 소스까지의 18개 구현 파일 변경이다.

## 금고 및 설정

WO05CompatibilityTests는 암호화 Lab 금고, 설정, 업데이트 및 롤백 이후 원본을 대조한다.
잘못된 비밀번호와 읽기 전용 내보내기 시험을 유지했다.
대화형 회수 CLI는 추가로 Lab v5에서 실행했다.
프로세스 종료 매트릭스의 vault-marker는 암호화 금고 시험이 아니다.
Lab v4의 새 대화형 CLI 실행 증거는 없다.
AV3 자동 회수, AV3 Writer와 자동 마이그레이션은 활성화하지 않았다.
내보내기 결과는 평문이다. 지정한 출력 경로에 파일이 남는다.
StringBuilder.Clear는 관리 메모리의 모든 비밀번호 복사본 제거를 보장하지 않는다.
회수 스냅샷과 평문 출력은 사용자 지정 위치에서 별도로 관리해야 한다.

## 후속 검토용 요청문

```text
WO-06 검증 소스 5647b352cf41082def74bef2701b6fe707c51cc0과 증거 문서를 함께 검토한다.
128개 관련 테스트는 앱 전체 769 PASS / 111 SKIP에 포함된다.
별도 프로세스 121개 명령은 합성 설치 시험이다.
실제 VM, UAC, 재부팅, 보호 ACL과 생산 서명은 별도 미완료 항목이다.
미기록 임시 파일 ID, WO-05 최초 전환, 구형 미완료 저널의 관리자 복구를 우선 검증한다.
각 수정은 실제 실패 재현과 회귀 시험으로 확인한다.
출시 판정은 실제 증거에 따라 다시 결정한다.
main 병합과 공식 릴리스는 진행하지 않는다.
```

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.

## 최종 보완 및 재검증

구형 미완료 저널의 파일·백업 진단을 추가했다.
추가 6개 시험으로 최종 관련 테스트는 128개, 앱 전체 통과는 769개로 증가했다.
새 진단의 첫 실행은 외부 경로 예외 처리 누락으로 1 FAIL / 127 PASS였다.
InvalidDataException 처리 범위를 수정한 뒤 128개가 통과했다.
같은 예외 누락은 손상된 도우미와 스냅샷의 복구 선택 경로에도 있었다.
해당 경로는 손상 후보를 거부하고 다음 인증된 복구 후보를 확인하도록 수정했다.
실제 서명 스냅샷 전환 E2E는 여전히 BLOCKED다.
최초 CI 38053004305는 이전 소스의 122개 관련 시험을 통과했다.
최종 CI는 38054469100과 소스 5647b352cf41082def74bef2701b6fe707c51cc0를 사용한다.
복원 중 저널과 Restored 저널의 별도 프로세스 종료 매트릭스를 추가했다.
최종 매트릭스는 121개 명령이다. 81개 정상 종료, 37개 의도한 강제 종료, 3개 예상 차단 결과를 확인했다.

## 최종 Windows CI 결과

CI 38054469100은 소스 5647b352cf41082def74bef2701b6fe707c51cc0에서 success로 완료됐다.
원격 TRX를 내려받아 128개 관련 시험, 전체 769 PASS / 111 SKIP, SecurityLab 36 PASS를 확인했다.
121개 프로세스 명령은 81개 정상 종료, 37개 의도한 강제 종료, 3개 예상 차단 결과를 확인했다.
Release 빌드는 경고 0개와 오류 0개를 기록했다.
실제 VM 설치와 생산 서명 항목은 BLOCKED 상태를 유지한다.
기존 복구 버튼은 도우미 실행 성공 후 ExitForUpdateHandoff를 호출한다.
이 앱 종료 연결은 코드로 확인했다. 실제 GUI 실행 증거는 없다.
