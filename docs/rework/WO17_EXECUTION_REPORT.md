# WO-17 — 실행 보고

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

검증 결과와 실제 출력은 evidence/wo17에 보존했습니다.
신규 WO-17 회귀는 39 PASS / 0 FAIL / 0 SKIP입니다.
SecurityLab은 36 PASS입니다. 최종 전체 집계는 FULL_TEST_RESULT.txt를 확인하십시오.
앞선 기준 CI 38419949642의 소스와 성공 상태를 GitHub API에서 다시 확인했습니다.
기준 앱 1,139 PASS / 111 SKIP 기록과 트리 일치도 유지했습니다.
로컬 최초 기준선은 sandbox testhost 연결 실패로 종료 1입니다.
기존 WO-11·WO-16 76개 재실행은 종료 0입니다.
중간 전체 시험은 새 빌드 DLL 잠금 충돌 때문에 종료했습니다.
해당 중간 실행을 최종 회귀 성공으로 집계하지 않습니다.
최종 빌드 후 전체 회귀를 별도로 실행했습니다.
NuGet 접근 오류, DLL 잠금 빌드 오류와 이후 재시도 결과를 별도로 보존했습니다.

실제 변경 파일:
- `.github/workflows/wo17-windows-validation.yml`
- `scripts/wo17-ui-source-validation.ps1`
- `src/SmartPerformanceDoctor.App/Services/Commercial/ForensicSecureDeleteEngine.cs`
- `src/SmartPerformanceDoctor.App/Services/Commercial/PathSafetyGuard.cs`
- `src/SmartPerformanceDoctor.App/Services/Commercial/ProfessionalSecureDeleteService.cs`
- `src/SmartPerformanceDoctor.App/Services/Commercial/SecureDeleteExecutionResult.cs`
- `src/SmartPerformanceDoctor.App/Services/Commercial/SecureDeletePreflight.cs`
- `src/SmartPerformanceDoctor.App/Services/Commercial/SecureDeleteStorageProfiler.cs`
- `src/SmartPerformanceDoctor.App/Services/SystemCare/StorageInventory.cs`
- `src/SmartPerformanceDoctor.App/Services/SystemCare/StorageTargetPolicy.cs`
- `src/SmartPerformanceDoctor.App/ViewModels/StorageCareViewModel.cs`
- `src/SmartPerformanceDoctor.App/Views/SecureDeleteCenterPage.xaml.cs`
- `tests/SmartPerformanceDoctor.Tests/WO17StorageDeleteTests.cs`
- `tools/SmartPerformanceDoctor.CleanupProbe/Program.cs`
- `tools/SmartPerformanceDoctor.CleanupProbe/SmartPerformanceDoctor.CleanupProbe.csproj`
- `tools/SmartPerformanceDoctor.StorageCareProbe/Program.cs`
- `tools/SmartPerformanceDoctor.StorageCareProbe/SmartPerformanceDoctor.StorageCareProbe.csproj`

소스 복사본 복원:
1. MODIFIED_FILE을 독립 wo17-rollback-* 디렉터리의 RESTORE_COPY로 복사하십시오.
2. ROLLBACK.sh에 해당 독립 파일 경로를 전달하십시오.
3. 복원 해시를 ORIGINAL_FILE 해시와 비교하십시오.
이 복원은 설치 트랜잭션·금고·삭제한 파일의 원복 시험이 아닙니다.
실제 합성 파일 원복은 QUARANTINE_RECOVERY_RESULT.json과 CRASH_RECOVERY_RESULT.json을 확인하십시오.

GitHub에 공개하지 않은 정보:
합성 원본과 감사 키는 로컬 생성 시험 폴더에만 보존했습니다.
실제 사용자 금고와 파일은 실행 시험에 사용하지 않았습니다.
GUI 실행, 디스크 전원 차단과 저사양 실물 시험은 NOT TESTED입니다.

CI 최초 실행 38435267878은 기존 CleanupProbe 소스 연결 누락으로 실패했습니다.
StorageTargetPolicy 연결을 추가했습니다. 보호 규칙을 완화하지 않았습니다.
시험 도구는 명시적인 합성 부모 디렉터리를 받도록 변경했습니다.
사용자 TEMP 재실행은 자동 승인 검토에서 거부됐습니다.
그 요청을 다시 수행하지 않았습니다. 작업 공간의 격리 디렉터리에서 정상 시험했습니다.
최종 소스 63febc9의 src 코드는 bc5c740과 동일합니다. 시험 도구·CI·신규 시험의 경로 계약만 변경했습니다.
로컬 중복 빌드는 생성된 XAML 파일 충돌도 발생시켰습니다. 이후 정상 빌드 결과를 별도로 보존했습니다.

## 최종 자동 검증

Windows CI 38436799177은 SUCCESS입니다. 검증 소스는 `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`입니다.
CI 링크: https://github.com/scarlel96-design/PC-Care/actions/runs/38436799177
앱 전체는 1,178 PASS / 0 FAIL / 111 SKIP / 1,289 TOTAL입니다.
신규 WO-17은 39 PASS입니다. 전체 앱 집계에 포함됩니다.
업데이트 128 PASS와 성능 233 PASS도 전체 앱 집계에 포함됩니다.
SecurityLab은 별도 36 PASS / 0 FAIL / 0 SKIP입니다.
로컬 전체 회귀도 같은 집계로 종료 코드 0을 반환했습니다.
기존 111 SKIP은 유지했습니다. Skip을 통과로 계산하지 않았습니다.
로컬 전체 제품 참조 빌드는 경고 0 / 오류 0입니다.
근거는 `PROBE_BUILD_FINAL.txt`입니다. 최종 시험 프로젝트 빌드도 경고 0 / 오류 0입니다.
CI 로그 재조회가 완료됐습니다. Release 빌드는 경고 0 / 오류 0입니다.
CI 빌드·복원 근거는 `CI_BUILD_RECOVERY_RESULT.txt`입니다. 테스트 출력은 `CI_FINAL_RESULT.txt`입니다.
최종 문서 커밋은 이 검증 소스와 구분합니다.
문서 커밋 이후 제품 소스 동일성은 Git 차이와 원격 트리로 확인합니다.

최초 증거 커밋 da9f20f의 원격 HEAD와 트리를 확인했습니다. 보고서 15개와 증거 54개가 원격에 존재했습니다.
후속 증거 갱신은 제품 소스를 변경하지 않습니다. 최종 HEAD는 제출 브랜치의 현재 ref로 확인하십시오.
