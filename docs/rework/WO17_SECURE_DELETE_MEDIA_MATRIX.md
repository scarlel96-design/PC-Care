# WO-17 — 매체별 보안 삭제 범위

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

NIST SP 800-88 Rev.2는 2025년 9월 최종판입니다.
참조: https://csrc.nist.gov/pubs/sp/800/88/r2/final

| 매체 | 현재 구현 | 검증 한계 |
|---|---|---|
| HDD | 논리 파일 CSPRNG 덮어쓰기·표본 검증 | 재매핑·VSS·백업·슬랙 전체 미검증 |
| SATA SSD | 논리 파일 Clear | 웨어 레벨링·오버프로비저닝 물리 삭제 미검증 |
| NVMe | 논리 파일 Clear | 장치 Sanitize/Purge 미실행 |
| Unknown | 보수적 1회 Clear | 매체 식별 신뢰도 제한 |
| 가상 디스크 | 파일 경계만 처리 | 호스트·스냅샷 잔존 미검증 |
| 클라우드·UNC | 경로 정책 차단 | 원격 사본 삭제 미지원 |

이 결과는 NIST 인증이나 장치 Purge 성공이 아닙니다.
물리 매체 복원 저항 시험은 NOT TESTED입니다.
VSS가 감지되지 않았다는 결과도 부재 증명으로 표시하지 않습니다.
