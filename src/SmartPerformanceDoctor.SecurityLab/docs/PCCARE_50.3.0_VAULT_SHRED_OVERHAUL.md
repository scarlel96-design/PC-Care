# PCCare 50.3.0 — 보안 금고 · 보안 삭제 대대적 개편

**제품:** PC 케어 프로 (SmartPerformanceDoctor / PCCare)  
**범위:** WinUI 앱 내 Secure Vault + Secure Delete (별도 Tauri 앱 아님)

## 보안 금고 v4 (`spd-vault-v4`)

| 항목 | 내용 |
|------|------|
| KDF | Argon2id 프로파일 Balanced / Strong(기본) / Extreme |
| 키 계층 | Password → Argon2id → KEK → VMK · metadata · mac (HKDF) · per-entry DEK |
| 객체 저장 | `objects/ab/{random32}.obj` — 원본 파일명 비노출 |
| 청크 AEAD | 1MiB 청크, 청크별 독립 nonce (AES-256-GCM) |
| 복구코드 | 10개 1회 표시, 디스크에는 SHA-256 해시만 |
| 레거시 | v2/v3 금고 열기·가져오기 호환 유지 |

## 보안 삭제 상용급

| 항목 | 내용 |
|------|------|
| 확인 문구 | 권장 `이 작업은 되돌릴 수 없습니다` (+ 기존 문구 호환) |
| dry-run 필수 | 대상 0개면 실행 거부 |
| 경로 가드 | 드라이브 무관 시스템 경로 · 정션 · 금고 저장소 차단 |
| SSD | crypto-erase → 난독화 순서 수정, ADS FindFirstStreamW |
| 정직 고지 | Level 5 허위 보증 금지, SSD 물리 한계 명시 |

## 버전

`Directory.Build.props` → **50.3.0**

## 테스트

`SecurityAttack` + `SecureVault` 필터 단위 테스트 실행.
