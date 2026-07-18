# SecurityLab 보안 감사 체크리스트

**대상:** SecurityLab (VaultV4 + ShredNext + Migration)  
**제품 탑재:** 기본 OFF / 현재 0%  
**일자:** 2026-07-12  

범례: ✅ 충족 · 🔧 이번에 보완 · ⏳ 후속 · ❌ 의도적 비구현

---

## 1. 암호·키

| 항목 | 상태 | 비고 |
|------|------|------|
| 자체 암호 알고리즘 없음 | ✅ | AES-GCM, Argon2id, HKDF/HMAC |
| AEAD 필수 | ✅ | 청크/메타/키 wrap 전부 태그 검증 |
| 비밀번호 → 파일키 직결 금지 | ✅ | KEK → VMK → wrap/meta |
| Argon2id 사용 | ✅ | LabFast는 테스트 전용 |
| 고정 nonce 금지 | ✅ | CSPRNG nonce |
| nonce 재사용 테스트 | ✅ | Lab tests |
| 키/비밀번호 zeroize | ✅ | KEK/DEK/평문 버퍼 ZeroMemory |
| 상수시간 비교 | ✅ | LabCryptoCompare (해시·복구코드·헤더) |
| 로그에 비밀 기록 금지 | ✅ | entry id / 해시만 |
| 복구코드 원문 디스크 저장 금지 | ✅ | SHA-256 해시만 · 일회 사용 |
| 무결성 실패 시 open 거부 | ✅ | header hash, content hash |
| 대용량 스트리밍 AEAD | ✅ | EncryptChunkedToFile / DecryptChunkedFromFile |
| 전체 평문 상주 최소화(import 경로) | ⏳ | ImportFile은 아직 전체 로드 (스트리밍 import 후속) |

## 2. 저장·메타

| 항목 | 상태 | 비고 |
|------|------|------|
| 파일명 평문 객체 경로 금지 | ✅ | objects/ab/random.obj |
| 메타 암호화 | ✅ | metadata.db.enc |
| 헤더 변조 감지 | ✅ | SHA-256 side-car |
| 임시 평문 파일 | ✅ | LabSecureTemp 마이그레이션 verify wipe |
| 감사 로그 체인 | ✅ | LabAuditChain SHA-256 해시 체인 |
| 무결성 스냅샷 | ✅ | objects + critical.header hashes |

## 3. 인증·남용 방지

| 항목 | 상태 | 비고 |
|------|------|------|
| 짧은/약한 비밀번호 거부 | ✅ | LabPasswordPolicy (+ 키보드 walk) |
| Unlock rate limit | ✅ | LabRateLimiter (자동 파괴 없음) |
| 세션 idle / max lock | ✅ | LabSessionPolicy |
| 실패 시 자동 데이터 파괴 | ❌ | 기본 금지 (정책) |
| 복구코드 일회성 | ✅ | TryConsume / ProveRecoveryCode |
| 마이그레이션 소스≠대상 경로 | ✅ | V3ToLabMigrator + Policy |
| Import 크기 한도 | ✅ | MaxImportBytes (기본 512MiB) |

## 4. 보안 삭제

| 항목 | 상태 | 비고 |
|------|------|------|
| dry-run 필수 | ✅ | |
| 확인 문구 | ✅ | LabPolicyEngine |
| 시스템·프로필·특수 폴더 루트 차단 | ✅ | LabSecurePath |
| UNC/네트워크 경로 차단 | ✅ | |
| 금고 경로 보호 | ✅ | shred/export |
| SSD 한계 고지 | ✅ | |
| 절대 복구 불가 주장 금지 | ✅ | |

## 5. 난독화·무결성 (합법 범위)

| 항목 | 상태 | 비고 |
|------|------|------|
| 정책 문서 | ✅ | OBFUSCATION_AND_HARDENING_POLICY.md |
| Release 심볼 제거 가이드 | ✅ | RELEASE_HARDENING_LAB.md |
| 바이너리/파일 해시 기록 | ✅ | LabIntegrityManifest |
| 디버거 탐지 시 경고만 | ✅ | LabHardeningProbe |
| 루트킷/AV 우회 | ❌ | 금지 |
| 문자열 난독화로 보안 대체 | ❌ | 금지 |

## 6. 정책 엔진

| 항목 | 상태 | 비고 |
|------|------|------|
| 파괴 작업 정책 게이트 | ✅ | LabPolicyEngine |
| AI가 키/삭제 직접 실행 금지 | ✅ | AI 미연결 |
| 외부 rule 서명 없이 적용 금지 | ✅ | LabRulePack HMAC |
| 제품 게이트 기본 OFF | ✅ | LabProductGate |

## 7. 제품 병합

| 항목 | 상태 | 비고 |
|------|------|------|
| 기본 OFF 플래그 문서 | ✅ | PRODUCT_FEATURE_FLAGS.md |
| App ProjectReference 없음 | ✅ | |
| LabProductGate 어댑터 계약 | ✅ | 호출 시 throw (OFF) |
| 마이그레이션 승인 절차 | ✅ | 본 문서 §8 |

## 8. 병합 승인 전 필수

- [x] SecurityLab 테스트 통과
- [x] dry-run + execute 마이그레이션 (Lab 대상 경로)
- [x] 보안 체크리스트 작성
- [x] 난독화/하드닝 정책 문서
- [x] 제품 게이트 OFF 자가점검 (`policy-selfcheck`)
- [ ] 사용자/소유자 명시 승인
- [ ] 제품 플래그 OFF 1버전 관찰 (App 어댑터)
- [ ] 병합 PR
