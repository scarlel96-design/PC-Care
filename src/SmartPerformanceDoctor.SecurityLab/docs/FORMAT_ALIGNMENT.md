# 포맷 정렬 문서 — 제품 v3 · SecurityLab v4 · astra-vault Rust

**목적:** 세 트랙의 저장 구조를 비교하고, 향후 통합 시 필드 매핑·갭을 명확히 한다.  
**제품 탑재:** 없음. SecurityLab / astra-vault 연구용.

---

## 1. 트랙 개요

| 트랙 | 식별자 | 구현 위치 | 상태 |
|------|--------|-----------|------|
| 제품 안정 | `spd-vault-v3` | `SmartPerformanceDoctor.App` SecureVault* | **설치본 사용** |
| SecurityLab | `spd-vault-v5-lab` (legacy id `spd-vault-v4-lab`) | `src/SmartPerformanceDoctor.SecurityLab` LabVault* | **제품 출시 경로 (50.4.x)** |
| astra-vault | AVLT1 (Phase1) | `astra-vault/crates/*` | Rust 연구, 미탑재 |

---

## 2. 디렉터리 레이아웃

| 역할 | 제품 v3 | Lab v4 | astra-vault |
|------|---------|--------|-------------|
| 루트 | `%LocalAppData%\...\secure_vault\default` | 사용자 지정 경로 | 사용자 지정 `.vault/` |
| 마커/헤더 | `vault.svdb` + `key_envelope.bin` | `vault.header.json` (+ blake3, backup) | `vault.header` (binary planned) |
| 메타데이터 | `vault_manifest.json.enc` | `metadata.db.enc` | `metadata.db.enc` |
| 객체 | `data/shard_*.blob` (+ `data/redundant/`) | `objects/ab/{id}.obj` | `objects/ab/{id}` (OBJ1) |
| 복구 | `recovery/recovery_envelope.bin` | `recovery/recovery_codes.v4.json` (해시) | `recovery/recovery_codes.hash` |
| 감사 | `audit/vault_audit.log.enc` | `audit/events.log` (비민감) | `audit/events.log` |
| 기타 | — | tombstones/, integrity/ | tombstones/, integrity/ |

---

## 3. 암호·키 계층

| 항목 | 제품 v3 | Lab v4 | astra-vault |
|------|---------|--------|-------------|
| KDF | Argon2id (기본 Strong급) / PBKDF2 레거시 | Argon2id 프로파일 | Argon2id 프로파일 |
| 내용 AEAD | AES-256-GCM (12B nonce) | AES-256-GCM 청크 (12B) | **XChaCha20-Poly1305 (24B)** |
| 청크 | 단층 layered 샤드 | 1 MiB 청크 `SPDCHK3` | 1 MiB 청크 `OBJ1` + 24B nonce |
| 키 분리 | KEK→vault/meta/mac · per-entry DEK | KEK→VMK→meta/wrap · per-entry DEK | KEK→VMK→meta/wrap/audit · file keys |
| 파일명 | 매니페스트 내 암호문 | 메타 암호문 (이름 평문은 언락 후 메모리) | 메타 암호문 |
| 디스크 객체명 | `shard_timestamp_id.blob` (일부 정보 노출) | 랜덤 32 hex | 랜덤 object id |

### 정렬 갭 (중요)

1. **AEAD:** Lab v5 content = **XChaCha20-Poly1305** (`SPDCHK4`) + AES-GCM wrap; astra/AV3 binary still separate.  
2. **청크 컨테이너 매직:** `SPDCHK4` vs `OBJ1` / AV3 — 바이트 변환 비권장, re-encrypt only.  
3. **헤더:** Lab=JSON dual-copy + activation 3-copy; AV3=binary planned — `LabToAv3MigrationGate` execute **denied**.  
4. **복구:** Lab=10 one-time VMK codes (v5 slots); 제품 v3=recovery envelope; AV3 recovery root 매핑 미실행.  
5. **Writer:** Lab write path only (`LabWriteGate`); AV3 ProductionWriter **always OFF**.

---

## 4. 필드 매핑 (개념)

| 개념 | 제품 v3 | Lab v4 | astra-vault |
|------|---------|--------|-------------|
| vault id | marker/envelope 내재 | `VaultId` hex | UUID |
| entry id | `EntryId` | `EntryId` | entry uuid |
| object id | ≈ `ShardName` | `ObjectId` | object_id |
| display name | EncryptedLabel | DisplayName (meta AEAD 안) | display_name |
| content hash | ContentSha256 | ContentSha256 | content blake3 (설계) |
| wrapped DEK | DekWrapped/Nonce/Tag | Dek*Hex | wrapped_data_key |
| blob format | BlobFormat 1/2 | 청크 전용 | OBJ1 |

---

## 5. 마이그레이션 원칙 (v3 → Lab v4)

**권장 방식: re-encrypt import (복사·재암호화)**  
- v3 언락 → plaintext export (메모리/임시) → Lab import → 해시 검증 → v3 보관  
- 헤더/샤드 바이트 단위 변환은 **비권장** (포맷 갭이 큼)

**금지:**  
- 제품 App에 Lab 프로젝트 참조  
- 사용자 승인 없는 원본 v3 삭제  

**dry-run 도구:** `V3MigrationDryRun` — 구조 스캔 + 예상 작업 목록 (복호화 없음 기본)

---

## 6. 정렬 로드맵

| 단계 | 내용 | 상태 |
|------|------|------|
| A | 본 문서 필드 표 | **완료** |
| B | Lab 청크 매직/헤더를 astra와 맞출지 결정 | 대기 |
| C | dry-run 마이그레이션 리포트 CLI/API | **구현** |
| D | 실제 re-import 실행기 (Lab only) | 미구현 |
| E | 제품 플래그 병합 | 승인 후 |

---

## 7. 정직성 메모

- 세 트랙 모두 “절대 보안”을 주장하지 않는다.  
- 잠금 상태 at-rest 보호 + 복구 난이도 향상이 목표다.  
- SSD 물리 삭제 보장은 어느 트랙도 하지 않는다.
