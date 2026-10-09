# PC-Care 프로젝트 감사 — Phase 0

기준: `codex/pccare-rework-phase0-1`, 시작 커밋 `95eafbfebe6189afa81c873e58ab65f1d29e50e0`. `origin/main`은 `dbc374a69829fe6d3fd5fc78394d9a9763a4b407`이며 시작 지점은 main보다 5커밋 앞선다. 이 브랜치는 기존 작업을 포함하며 main과 릴리스를 수정하지 않는다.

## 구성

정적 인벤토리: `src` 파일 716개, C# 프로젝트 9개, `Views` XAML 38개, `tests`의 `*Tests.cs` 62개. 이 수치는 파일 수이며 각 화면의 실제 사용자 노출 여부를 의미하지 않는다.

- `src/SmartPerformanceDoctor.App`: WinUI 셸, 시스템 케어, 통합 점검, 업데이트, 보안 UI. 단일 앱 프로젝트가 다수 기능을 묶는다.
- `src/SmartPerformanceDoctor.AstraVault`: AV3 읽기/쓰기 설계, 단계 게이트, 저널 및 내구성 계층.
- `src/SmartPerformanceDoctor.SecurityLab`: 보안 삭제와 저장장치 보안 실험 계층.
- `src/SmartPerformanceDoctor.Aegis*`, `Contracts`, `Data`: 방어, 복구 서비스, 계약 및 데이터.
- `src/SmartPerformanceDoctor.Setup`: 별도 설치기 프로젝트. 앱의 설정/업데이트와 화면·문구 정렬이 필요한 경계다.
- `tests/SmartPerformanceDoctor.Tests`, `SecurityLab.Tests`: 단위·통합·시뮬레이션 테스트. 테스트의 이름과 실제 게이트 값이 일치하지 않는 사례가 있다.
- `Directory.Packages.props`: 중앙 NuGet 버전. Windows App SDK 2.2.0 사용. 의존성 업그레이드는 별도 호환성·빌드 검증 후 진행한다.

공식 [Windows App SDK 다운로드 페이지](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)는 2026-09-16 안정 채널 2.5.1을 표시한다. 2.2.0에서 즉시 올리면 WinUI API·설치기·테스트 호환이 바뀔 수 있으므로 별도 브랜치와 실행 검증이 필요하다.

## 우선 확인 결과

1. 기존 업데이트 검사기는 매니페스트 목록 지문이 맞으면 ZIP 실제 파일을 해시하지 않았다. 독립 재현에서 변조 파일과 `payload/../escape.dll`이 모두 `READY`였다. Phase 1 패치와 회귀 프로브는 `UPDATE_SECURITY_AUDIT.md`에 기록한다.
2. 설치 단계는 ZIP 항목 경로를 그대로 조합해 추출했고, `inspection.IsValid`만으로 해시 재검증을 건너뛸 수 있었다. 패치에서 목록 기반 추출과 두 번의 내용 검증을 추가했다.
3. `Av3PhaseGate`의 프로덕션/외부 검토 플래그가 `true`인데 문서·일부 이름은 비활성 상태를 전제한다. 실제 앱은 `AstraVaultHostService`에서 레거시 `SecureVaultService`로 라우팅한다. 게이트 값 자체를 이번 패치에서 추측으로 변경하지 않는다.
4. 런타임 서명 검사 우회가 `RuntimeTrustLevel.Full`에 연결된다. 검증 사실과 사용자 허용 상태를 분리해야 한다.
5. `CareRollbackService.Rollback`은 복원 위치에 생긴 새 파일을 삭제하고 격리본으로 덮어썼다. Phase 1에서는 새 파일과 격리본을 모두 보존하며 충돌을 실패로 보고하도록 변경했다.
6. `BuiltInProductModules`에는 보안 보조 기능인 장치 PURGE 준비가 1차 내비게이션으로 등록되어 기본 5개 메뉴 계약과 충돌했다. 보조 기능으로 정리했다.

## 현재 증거 범위

정적 코드 검사와 독립 ZIP 재현은 수행했다. 앱 전체 빌드, UI 실사용, 설치기/업데이트 실기기, 실제 금고 데이터 호환, 보안 삭제 물리 매체 잔존성은 각각 별도 결과로 기록한다. 이 문서는 제품 전체의 보안·성능 보증이 아니다.
