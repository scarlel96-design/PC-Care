# PCCare 51 아키텍처

## 목표

51.0.0은 화면마다 기능을 직접 연결하던 구조를 제품 영역, 기능, 실행 엔진의 세 층으로 분리한다. 새 기능을 추가할 때 메인 창의 조건문을 늘리지 않고 모듈 카탈로그에 기능과 엔진을 등록하는 것이 핵심이다.

## 제품 구조

```text
PCCare Shell
├─ Home
├─ Care
│  ├─ Unified diagnostics
│  ├─ System care
│  └─ Driver / audio diagnostics
├─ Security
│  ├─ AstraVault
│  └─ Secure delete
├─ History & recovery
└─ Settings
```

`Platform/ProductArchitecture.cs`가 다음 계약을 정의한다.

- `IProductModule`: 한 제품 모듈이 제공하는 기능과 엔진 묶음
- `ProductFeatureDescriptor`: 페이지, 검색어, 표시 순서, 최소 사용자 모드, 설치 기능 의존성
- `EngineDescriptor`: 엔진 종류, 버전, 기능(capability), 관리자 권한 및 별도 프로세스 여부
- `ProductCatalog`: 중복 ID와 잘못된 페이지 형식을 거부하고 탐색 메뉴를 구성하는 단일 레지스트리

기본 구성은 `Platform/BuiltInProductModules.cs`에 있다. 외부 DLL을 임의로 로드하는 플러그인 방식은 PC 관리·보안 프로그램의 공격 표면을 넓히므로 사용하지 않는다. 확장은 검토와 테스트를 거친 컴파일 타임 모듈 등록을 원칙으로 한다.

## 기능 또는 엔진 추가

1. 기존 제품 영역에 속하면 해당 `IProductModule`에 descriptor를 추가한다.
2. 독립 배포 단위라면 새 `IProductModule` 구현을 만든다.
3. 설치 선택 기능은 `InstallFeatureId`로 연결한다.
4. 관리자 권한이나 별도 프로세스가 필요하면 `EngineDescriptor`에 명시한다.
5. `ProductComposition.CreateDefault()`에 모듈을 등록하고 카탈로그 계약 테스트를 추가한다.

ID는 `영역.기능` 또는 `engine.이름` 형식의 안정된 문자열을 사용한다. 기존 ID의 의미를 바꾸지 말고 새 ID를 추가한다.

## UI 원칙

- 최상위 탐색은 홈, PC 최적화, 보안, 기록 및 복구, 설정의 5개 영역만 유지한다.
- 자주 쓰지 않는 전문 기능은 해당 영역의 `Expander` 아래에 둔다.
- 점검은 범위 선택 → 검사 → 결과 검토 → 조치의 동일한 흐름을 사용한다.
- 예시 수치를 실제 측정값처럼 표시하지 않는다. 검사를 하지 않았다면 `점검 전`으로 표시한다.
- 삭제, 복구, 업데이트처럼 상태 변경이 큰 작업은 사용자의 명시적 동작 뒤에만 수행한다.

디자인 토큰은 `Resources/PccDesignSystem.xaml`에 모으며 기존 화면의 점진적 이행을 위해 과거 스타일 키에 호환 alias를 제공한다.

## 실행 및 권한 경계

- 일반 실행은 표준 사용자 권한을 유지한다.
- `PCCARE_REQUIRE_ADMIN=1`은 진단용 명시 설정이며 기본 동작이 아니다.
- 보류 업데이트는 정상 시작 중 자동으로 UAC를 띄우거나 앱을 닫지 않는다.
- 업데이트 화면에서 사용자가 `업데이트 마무리`를 누른 경우에만 별도 프로세스로 handoff한다.
- UAC 취소 또는 프로세스 시작 실패 시 현재 앱을 유지하고 보류 상태를 남긴다.

## 기술 기준

- .NET 10 / C# 최신 언어 버전
- Windows App SDK 2.2 stable
- WinUI 3, self-contained x64 배포
- Rust 2024 edition 기반 Core 및 Repair Helper
- 해시·매니페스트 검증 기반 개인 배포(Authenticode 서명 비필수)

Windows App SDK는 NuGet에 더 높은 번호가 보이더라도 Microsoft의 stable 채널을 기준으로 선택한다.

- [Windows App SDK 최신 다운로드](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
- [Windows 앱 SDK 및 OS 버전 선택 안내](https://learn.microsoft.com/windows/apps/get-started/versioning-overview)

## 검증 게이트

- x64 Release 솔루션 빌드: 경고와 오류 0개
- 제품 카탈로그 ID/페이지/엔진 capability 계약 테스트
- 정상 시작 시 자동 업데이트 UAC 금지 정책 테스트
- 전체 .NET 회귀 테스트
- Rust workspace release test와 core smoke
- self-contained publish 레이아웃 및 비관리자 실행 smoke

