# PC 케어 프로 (PCCare) v50.5.0

Windows PC의 상태 점검, 안전한 정리, 드라이버·오디오 문제 진단, 보안 금고와 보안 삭제를 하나로 제공하는 로컬 PC 관리 프로그램입니다.

## 주요 기능

| 영역 | 제공 기능 |
|---|---|
| 시스템 케어 | 임시 파일·캐시·휴지통·레지스트리 경로·바로가기·시작 프로그램·서비스·디스크·네트워크·Windows 보안 상태 점검 |
| 정리 결과 | 검사 후 정리 대상, 정상 항목, 검사 완료 수를 요약하고 실제 조치가 필요한 항목만 목록으로 표시 |
| 통합 점검 | 빠른/시스템/드라이버/오디오/전체 점검과 단계별 진단·안전 복구 흐름 |
| 드라이버·오디오 | 장치 오류 코드, 오디오 서비스·장치 상태, 안전한 복구 제안 |
| 보안 금고 | AstraVault 기반의 로컬 암호화 금고, 복구·무결성 검증 설계 포함 |
| 보안 삭제 | 선택한 파일을 일반 복구가 어렵도록 삭제하며, 대상 경로 검증과 감사 기록을 남김 |

## 50.5.0 하이라이트

- 시스템 케어 검사 범위를 브라우저 개인정보 흔적, 예약 작업, SmartScreen 보호, HOSTS 리디렉션까지 확대했습니다.
- 검사 결과를 `정리 대상 / 정상 항목 / 검사 완료` 요약과 간결한 정리 목록으로 재구성했습니다.
- 통합 점검은 최근 처리 단계 중심으로 정리해 진행·결과 화면의 복잡도를 줄였습니다.
- 업데이트는 실행 중인 앱 내부 복사를 없애고 별도 마무리 프로세스로 처리하며, 설치 파일 기준 버전 검증을 강화했습니다.
- 최신 소스에 누락돼 있던 AstraVault 검증 도구와 Target 소스를 함께 반영했습니다.

## 설치와 업데이트

- 설치 파일: GitHub Releases의 `PCCare_Setup_v*.exe`
- 업데이트 파일: `PCCare_Update_v*.spdup`
- 앱의 **업데이트** 화면에서 패키지 검사 후 적용할 수 있습니다. 실행 중인 파일은 앱 종료 후 별도 프로세스가 안전하게 교체합니다.
- 릴리스에는 SHA-256과 `UPDATE_CHANNEL.json`이 함께 제공됩니다. 이 빌드는 개인 배포용이므로 Authenticode 서명 대신 패키지 해시 검증을 사용합니다.

## 개발 빌드

```powershell
.\scripts\build.ps1 -SkipInstaller
```

서명을 생략한 설치·업데이트 패키지 빌드:

```powershell
.\scripts\build.ps1 -SkipTests -SkipSigning
```

## 검증 상태

50.5.0 기준 x64 Release 빌드가 통과했고, 전체 직렬 회귀 테스트 결과는 **542 passed / 116 skipped / 0 failed**입니다. 보류 테스트는 AstraVault의 아직 의도적으로 활성화하지 않은 생산용 쓰기·마이그레이션 경로를 검증하는 항목입니다.

## 런타임 데이터

- 지식 DB: `%LOCALAPPDATA%\SmartPerformanceDoctor\data\knowledge.db`
- 업데이트 상태: `%LOCALAPPDATA%\SmartPerformanceDoctor\updates\`
- Aegis Mirror: `%ProgramData%\AstraCare\AegisMirror\`
- 검사 보고서: 설치 폴더의 `reports\`

## 저장소 구성

| 경로 | 용도 |
|---|---|
| `src/` | 앱·서비스·네이티브 엔진 소스 |
| `experimental/SecurityLab/` | 보안 금고·보안 삭제 보강 계층 |
| `tools/` | AstraVault 골든 벡터 등 검증 도구 |
| `tests/` | 단위·회귀 테스트 |
| `scripts/` | 빌드·패키징·검증·업데이트 스크립트 |
| `updates/` | 업데이트 변경 기록 |