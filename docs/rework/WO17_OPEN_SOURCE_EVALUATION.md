# WO-17 — 기술 선택

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

새 패키지는 추가하지 않았습니다. 기존 WindowsFileBoundary와 SandboxRecovery를 재사용했습니다.
.NET SDK 10.0.401과 Microsoft.WindowsAppSDK 2.2.0의 기존 통합을 유지했습니다.
Microsoft.CsWin32 0.3.358은 기존 프로젝트 바인딩으로 유지했습니다.
이번 파일 할당 조회는 작은 Win32 계약을 App Adapter에 추가했습니다.
Windows SDK FILE_STANDARD_INFO는 Windows Vista 이후 데스크톱 API입니다.
API 계약은 공식 문서에서 확인했습니다. 새 암호화 알고리즘은 없습니다.
Restart Manager는 파일 사용자 조사 후보입니다. 자동 프로세스 종료 기능을 채택하지 않았습니다.
기존 WinUI ListView와 ObservableObject는 현재 512개 목록 예산에 적합합니다.
새 MVVM 의존성의 실익이 없어 기존 구조를 유지했습니다.
새 의존성이 없다는 사실은 기존 패키지 전체의 취약점 부재 증명이 아닙니다.
NuGet 보안 감사 설정은 유지했습니다. 조회 실패와 성공한 복원 로그를 별도로 보존했습니다.
공식 근거:
- https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_standard_info
- https://learn.microsoft.com/en-us/windows/win32/rstmgr/about-restart-manager
- https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/listview-and-gridview
