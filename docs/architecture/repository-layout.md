# 저장소 폴더 규칙

PCCare 저장소는 소스와 실행 배포본을 분리합니다. 저장소 최상단에 DLL, XBF, 언어 리소스 또는 실행 파일을 직접 게시하지 않습니다.

| 경로 | 책임 |
|---|---|
| `src/` | 실제 제품에 포함되는 앱, 서비스, 보안 및 복구 엔진 |
| `tests/` | 단위, 계약, 회귀 및 보안 테스트 |
| `tools/` | 제품에 포함되지 않는 개발·검증 CLI |
| `scripts/` | 빌드, 게시, 설치, 업데이트, 검증 자동화 |
| `content/` | 검사 규칙, 상업 데이터, 아이콘과 정적 콘텐츠 |
| `docs/` | 아키텍처, 보안, UI 문서 |
| `updates/` | 버전별 변경 로그와 업데이트 메타데이터 |
| `artifacts/runtime/` | `publish-runtime.ps1`가 만드는 로컬 실행 배포본 |
| `artifacts/installer/` | 설치 프로그램과 설치 스테이징 산출물 |

## 유지 규칙

- .NET의 `bin/`, `obj/`, Rust의 `target/`, 모든 `artifacts/`는 생성물이며 Git에서 제외합니다.
- 앱이 참조하는 제품 프로젝트는 `src/`에 둡니다. 보안 금고·보안 삭제 보강 계층도 정식 제품 의존성이므로 `src/SmartPerformanceDoctor.SecurityLab/`에 둡니다.
- 테스트 실행 파일은 `tests/`, 개발용 CLI는 `tools/`에 둡니다.
- 릴리스 파일은 데스크톱의 버전 폴더와 GitHub Releases에 배치하고 소스 루트에는 복사하지 않습니다.
- 새 경로를 추가할 때 설치·업데이트 스크립트에서 하드코딩하지 말고 `scripts/RuntimeLayout.ps1`의 공통 경로 함수를 사용합니다.