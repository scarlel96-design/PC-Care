# WO-02 런타임 신뢰 검토

판정: **PARTIAL**. 신뢰 상태는 `Verified`, `UnsignedAllowed`, `Unverified`, `Failed`로 분리했다. 서명 검사를 건너뛰는 현재 소비자 모드는 `UnsignedAllowed`로 표시하며 `IsFullyTrusted=false`다. 기능 허용 정책은 별도 `IsOperationallyAllowed`로 유지해 기존 무서명 소비자 빌드의 기능을 계속 사용할 수 있다.

`RuntimeAuthenticodeVerifier`는 Windows [WinVerifyTrust](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust) 정책 검증을 호출한다. Subject 문자열 포함 여부로 신뢰하지 않는다. 유효한 제3자 서명도 PC-Care 발행자 핀이 없으면 `Unverified`다. 손상·신뢰 정책 실패는 `Failed`, 서명 없음은 `Unverified`다. 공식 발행자 인증서 핀과 교체/폐기 정책이 현재 비어 있으므로 생산 `Verified`는 달성하지 않았다.

남은 검증: 정상 서명, 깨진 서명, 변조, 무서명, API 오류의 실제 파일 fixture와 설치 환경 E2E. 이 빌드에서 실제 사용자 설치 파일의 서명 신뢰를 확인했다고 주장하지 않는다.
