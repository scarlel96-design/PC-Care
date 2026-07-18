# Astra Vault Gap Report

**판정: NO-GO** (Writer Enable Readiness — production AV3 writer **NOT AUTHORIZED**)  
**현재 Phase:** **E-14 완료** — Disk durability review package (candidate only); `ActualDiskDurabilityReviewed=false`; writer **NOT AUTHORIZED**; `ProductionEnableAuthorized=false`; S-Class **NOT YET SATISFIED**  
**게이트:** `ProductionWriterEnabled` / `JournalWriterEnabled` / `MigrationEnabled` / `WriterEnableReady` = **false**  
**S-Class Target:** **NOT YET SATISFIED** · legacy spd-vault **BELOW S-CLASS** (on-disk unchanged)

## 현재 구현 (PCCare `SecureVaultService`, format `spd-vault-v3`)

| 영역 | 현황 | 목표 대비 |
|------|------|-----------|
| KDF | Argon2id(신규) + PBKDF2(레거시) | ✅ 방향 일치, 최소 파라미터 게이트 필요 |
| AEAD | AES-256-GCM | ⚠️ XChaCha20-Poly1305 우선 미적용 |
| 키 계층 | KEK→vaultKey, HKDF metadata/mac/shard | ⚠️ VMK/DEK/index/journal/recovery 분리 불완전 |
| 메타데이터 | manifest 전체 AEAD 파일 | ✅ 잠금 시 평문 manifest 없음 |
| 파일명/경로 | manifest 내 필드 암호화 | ✅ unlock 전 복원 불가 |
| 콘텐츠 | per-entry DEK, layered shard MAC | ✅ AEAD + MAC (레거시 blob 호환) |
| 디스크 레이아웃 | `data/shard_타임스탬프_*.blob` 평탄 | ❌ chunk hash 경로·avpack·pack 없음 |
| 샤드 이름 | 타임스탬프+entryId 접두 노출 | ❌ 무작위 object id 요구 미충족 |
| 헤더 | 단일 `key_envelope.bin` | ❌ 3-copy authenticated header 없음 |
| Locator | `vault.svdb` 마커 | ❌ `vault.locator` bounded format 없음 |
| 트랜잭션 | manifest 즉시 덮어쓰기 | ❌ journal·generation·crash-safe commit 없음 |
| 인덱스/썸네일 | 없음 | ✅ 평문 인덱스 없음 (기능 부재) |
| 원본 처리 | 기본 `sealOrigin=true` (UI 해제 가능) | ⚠️ 레거시 정책; AV3 writer 없음 |
| Origin seal | desktop.ini·한글 readme·stub | ❌ “폴더락” 성격, 암호화 대체 아님 |
| Recovery | DPAPI 래핑 envelope | ⚠️ 이식성·VMK 래핑 모델과 불일치 |
| Sentinel | 없음 | ❌ Phase G |
| VaultGate (Rust) | Linux LUKS 드라이브 | 별 제품선, Windows 파일 금고와 미통합 |

## Critical / High

1. **High** — crash-safe writer 없음 → 손상·partial write 시 복구 미보장  
2. **High** — 저장소가 VeraCrypt급 container 구조 아님  
3. **High** — generation/rollback 탐지 없음  
4. **Medium** — 샤드 파일명·개수·크기 leakage  
5. **Medium** — export 시 일반 경로 평문 (temp 정책 미흡)  
6. **Low** — ChaCha 미사용 (GCM은 허용 옵션)

## Phase D (완료)

- metadata.root ciphertext AEAD authenticate/decrypt (read-only)  
- `MetadataRootReadOnlyReader` + golden vector extension  

## Phase E-0 (완료)

- `ASTRA_VAULT_WRITER_GATE.md` + crash-safe / journal / FI / risk register  
- `Av3PhaseGate`: design locks **true**, production writer **false**  

## Phase E-1 (완료)

- `FaultInjection/` + `Experimental/` + `Journal/` (descriptor validator, commit simulation)  
- `Av3FaultInjectionTests` — matrix-aligned classifications  
- Production writer / journal writer / migration **여전히 비활성**

## Phase E-2 (완료)

- `Av3HarnessCommitCrypto` + `Av3HarnessPostCommitAuthenticator` — activation/metadata AEAD + digest/commitment/generation chain  
- `Av3ProcessKillHarness` / `Av3FlushFaultHarness` / `Av3DurabilitySimulationMode`  
- `Av3FaultMatrixRunner` + `Av3FaultMatrixReport` — 15 fault points + 21 crash-safe scenarios (machine-readable, no secret leak)  
- `Av3TestStorage` — `av3-e2-` isolated temp prefix, path-escape block  
- `Av3PhaseGate.HarnessRealCryptoEnabled = true`; `ProductionWriterEnabled` **여전히 false**

## Phase E-3 (완료)

- `Av3ChildProcessKillHarness` + `SmartPerformanceDoctor.AstraVault.KillWorker` (Windows child-process kill)  
- `Av3DurableStorageHarness` / `Av3DurableFlush` (isolated temp only)  
- `Av3HeaderCopyWriterHarness` + `Av3HeaderCopyRecoveryClassifier` (test-only 3-copy skeleton)  
- `Av3RepairClassifier` — classification only, **no auto repair**  
- `Av3ActualKillMatrixRunner` — simulated vs actual compare report  

## Phase E-4 (완료 — High Risk Closure Gate)

- `Av3TornWriteSimulator` / `Av3AtomicWriteValidator` — R1 partial/torn write closure (harness)  
- `Av3HeaderRepairClassifier` / `Av3HeaderRedundancyReport` — R2/R3 classification + repair **plan only**  
- `Av3RollbackClassifier` — R10 rollback evidence  
- `Av3JournalConfidentialityScanner` — R11 cleartext path/filename rejection  
- `Av3PhaseGate.HighRiskClosureGateLocked = true`; **writer/migration flags remain false**  
- **R9:** migration stays Phase H; no spd-vault on-disk changes from E-4 harness  

## Phase E-5 (완료 — Production Writer Design Package)

- `ASTRA_VAULT_PRODUCTION_WRITER_DESIGN.md` + review package + `WriterDesign/IAv3*` (interfaces only)  
- `ASTRA_VAULT_ANCHOR_MODEL.md` / `ASTRA_VAULT_XCHACHA24_MIGRATION_PLAN.md` — **not implemented**  
- `ExternalReviewPackageReady=true`; `ExternalReviewCompleted=false`  
- **Writer Enable Readiness: NO-GO** — harness CLOSED ≠ production enable  

## Phase E-5.1 (완료 — external review fixes)

- GAP_REPORT / checklist semantics / App reflection tests / `SecretNonLeakPass` gate linkage  

## Phase E-7 (완료 — Pre-Enable Hardening)

- Multi-layer writer gate fail-closed (`Av3WriterAccessGate.EnsureProductionRouteFailClosed`, policy enforce)
- `Av3WriterInvariant*` + `Av3HarnessCommitGuardRegistry` + cancellation reports
- Production route negative matrix (factory, orchestrator, journal, durable, coordinator, App/Host/VM)
- Repair/recovery: **no auto-mutation** (`PerformsAutomaticRepair=false`)
- E-11.1: B-1 **PARTIAL / SIGNED CANDIDATE**; E-13: trusted provider package **COMPLETE** (harness); `ProductionAnchorImplemented=false`; **same-disk untrusted anchor alone cannot prove full-vault rollback resistance.**
- E-12.1: B-2 **APPROVED CANDIDATE**; `E121XChaCha24SignoffGateComplete=true`; `XChaCha24SignoffApprovedCandidate=true`; `XChaCha24Implemented=false`; ChaCha12 **BELOW S-CLASS**; S-Class still open.

## Phase E-9 (완료 — External Sign-off Prep)

- `ASTRA_VAULT_EXTERNAL_REVIEW_CHECKLIST.md`, `ASTRA_VAULT_EXTERNAL_REVIEW_EVIDENCE_INDEX.md` — reviewer artifacts
- Package refreshed with E-6–E-8 evidence; **latest verified** full suite **477 PASS** (E-10 SOT; historical 455 E-9.1) (see `ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md`; historical gap note 438 superseded)
- **external-review-ready** — not `ExternalReviewCompleted`, not writer enable

## Phase E-8 (완료 — Limited Dry-Run)

- `DryRun/Av3DryRunRunner` + synthetic fixtures — E2E commit on `av3-e8-` / `av3-harness-` only
- Read-only revalidation (`Av3DryRunReadOnlyRevalidator`) + invariants + telemetry scanner
- Fault matrix in `Av3PhaseE8Tests`; service/UI/DryRun namespace **not wired**
- Dry-run success **does not** set `WriterEnableReady` or authorize production writes

## Phase E-7.1 (완료 — Review Fixes)

- `ValidateTrustedGenerationPreserved` enforced; FailCleanup ↔ `ValidatePipelineResult`
- Negative matrix: migration, `TryOpenProductionSession`, non-harness session, `CommitThreeCopyAsync`, journal gate
- `AllWriterGatesClosed` includes `JournalWriterEnabled=false`
- Harness root: OS temp + canonical prefix list; reject user workspace paths
- Caller `CancellationToken` + repair classifier storage non-mutation tests
- E-7 engineering review: Critical/High none; **E-8 = limited dry-run / RC hardening**; writer enable **NO-GO**

## Phase E-6.2 (완료 — Review Fixes)

- `FailCleanup` simulation + `Av3CommitCleanupPosture` — post-auth trust vs `Committed` separation
- `Av3RecoveryClassifier` — cleanup failure after auth → `RecoveryRequired` (not `NewGenerationOpen`)
- E-6/E-4 classifier alignment tests (stale/high gen, rollback evidence)
- `Av3CommitJournalRecorder` harness digests → `Av3JournalDeterministicFixtures`
- `ScanUtf8TextualSurface` API; JNAL bytes → structural scan only
- `CleanupFailureHarnessCovered` / `E6ReviewFixesApplied` = **true**; writer enable **NO-GO**

## Phase E-6.1 (완료 — R11 Journal Leak Scanner Stabilization)

- `Av3JournalConfidentialityScanner` — structural JNAL parse + field allowlist; **no UTF-8 token scan on digest bytes**
- `Av3JournalTextualLeakScanner` — trace/report/exception only
- `Av3JournalDeterministicFixtures` — fixed hex digests; 1000× scan stability tests
- R11 RNG false-positive: **CLOSED** (harness)
- Production writer: **NOT AUTHORIZED**

## Phase E-6 (완료 — Disabled Production Writer Implementation)

- `Commit/`: orchestrator, transaction coordinator, durable store, header committer, journal recorder, recovery manager, write policy  
- `Av3WriterAccessGate` / `Av3WriterHarnessFactory` — production create **blocked**; harness-only  
- 14-step commit pipeline + fail-closed flush/reread/auth (harness FI)  
- **Not connected:** SecureVaultService, AstraVaultHostService, UI, user import/export, spd-vault migration  
- `ProductionWriterEnabled` / `JournalWriterEnabled` / `MigrationEnabled` / `WriterEnableReady` = **false**  
- AV3 **NOT PRODUCTION**; S-Class **NOT YET SATISFIED** (anchor + XChaCha24 not implemented)  

## 다음 작업

- Phase E-6+: production route enable review (external review + decision record **GO** 전 **금지**)
- `SecureVaultService`: 레거시 어댑터로 유지, av3 병행 후 migration (Phase H)  
- UI: **아스트라 금고** 브랜딩 + 보안 상태 enum + import 시 원본 유지 기본