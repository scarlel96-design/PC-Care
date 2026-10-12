# WO-15 앵커 기술 평가

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 채택과 근거
새 암호화 알고리즘과 패키지를 도입하지 않았다. 기존 .NET ProtectedData와 VaultFileBoundary를 재사용했다.
DPAPI CurrentUser를 제한된 Windows 시험 앵커에 채택했다. 실제 보호·해제·재열기·손상 거부를 실행했다.
DPAPI는 보호 데이터에 MAC을 제공한다. 같은 사용자와 컴퓨터에 연결된 보호 기능이다.
DPAPI는 단조 증가 카운터가 아니다. 인증된 과거 blob의 재생을 자체 거부하지 않는다.
공식 근거: [CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata).

| 기술 | 구현 상태 | 판단 |
|---|---|---|
| DPAPI CurrentUser | 시험 구현 PASS | 금고만 재생하는 제한 모델에 사용 |
| ACL + 별도 호스트 저장소 | PARTIAL | 일반 파일 경계 재사용; 독립 서비스/SID 보호 없음 |
| CNG 보호 키 | NOT IMPLEMENTED | 키 보호만으로 전체 상태 재생 방어를 증명하지 못함 |
| TPM 보호 키/상태 | NOT IMPLEMENTED | 장치별 API·권한·초기화·교체·지속성 시험 필요 |
| 외부 Witness | NOT IMPLEMENTED | 서버 최신 세대 CAS, 요청 인증, 장애 복구와 개인정보 정책 필요 |

TPM의 키 보호 지원을 일반적인 재생 방지 카운터의 존재로 해석하지 않았다.
공식 근거: [TPM fundamentals](https://learn.microsoft.com/en-us/windows/security/hardware-security/tpm/tpm-fundamentals).
외부 Witness는 선택형 후속 후보다. 이번 구현은 네트워크 요청을 만들지 않는다.
Witness에는 금고 ID·세대 증명만 전달하는 정책이 필요하다. 콘텐츠와 비밀번호 전송은 제외한다.
기기 교체와 백업 복원은 기존 코드의 재사용 권한과 별도 승인 절차가 필요하다.
TPM 수명·성능·재설치 시험과 원격 서버 실증 결과는 없다.

## 의존성 정리
.NET 10 앱의 중복 PackageReference 세 개를 제거했다.
System.IO.Pipes.AccessControl, System.Security.Cryptography.ProtectedData, System.Drawing.Common이 대상이다.
SecurityLab의 별도 ProtectedData 참조는 유지했다. 다른 TFM을 임의 변경하지 않았다.
Restore 0 warnings와 Release 0 warnings / 0 errors를 실제 확인했다.
[NU1510](https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1510)의 원인을 처리했다. 경고 억제는 추가하지 않았다.
