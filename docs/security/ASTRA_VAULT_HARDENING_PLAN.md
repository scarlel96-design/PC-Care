# Astra Vault Hardening Plan (Phase A–J)

| Phase | 상태 | 내용 |
|-------|------|------|
| A | **완료** | 감사·갭·레거시 정책·`SmartPerformanceDoctor.AstraVault` 스캐폴드 |
| B-1 | **완료** | 바이트 스펙, header 3-copy/slot/metadata parser, read-only unlock validator |
| B-2 | **완료** | Vector corpus, activation payload AEAD auth, pipeline ordering, parser safety |
| C | **완료** | Golden vector generator + `test-vectors/av3` LOCKED |
| D | **완료** | metadata.root ciphertext AEAD read-only validation (no writer) |
| E-0 | **완료** | Writer design gate (state machine, journal, crash matrix, FI plan) |
| E-1 | **완료** | FI harness + experimental writer skeleton (test-only) |
| E-2 | **완료** | Crypto-linked FI harness (activation/metadata AEAD, flush/kill simulation, automated matrix) |
| E-3 | **완료** | Actual child-process kill FI + durable harness + 3-copy header skeleton (test-only) |
| E-4 | **완료** | High Risk Closure Gate — torn write, header redundancy/conflict, rollback, journal confidentiality (**NOT writer enable**) |
| E-5 | **완료** | Production writer design + external review package + anchor/XChaCha plans (**NOT AUTHORIZED**; checklist = NO-GO) |
| E-6 | **완료** | Disabled production writer implementation (`Commit/*`, harness-only, gates **false**) — **NOT** App/UI/service wired |
| E-6.1 | **완료** | R11 journal leak scanner stabilization (binary/textual 분리, deterministic fixtures) |
| E-6.2 | **완료** | E-6/E-6.1 review P1/P2 fixes (cleanup FI, classifier tests, deterministic journal digests, ScanUtf8 footgun) — **NOT** writer enable |
| E-7 | **완료** | Pre-enable hardening (multi-layer gates, negative matrix, invariants, cancellation/concurrency) — **NOT** writer enable |
| E-7.1 | **완료** | E-7 review P1/P2 fixes (trusted-gen invariant, harness root policy, extended negative matrix, repair non-mutation tests) — **NOT** writer enable |
| E-9.1 | **완료** | Formal review M-01/M-02 — test evidence SOT; per-root commit guard leases — **NOT** writer enable |
| E-8 | **완료** | Limited dry-run (`DryRun/*`) E2E + revalidation + FI matrix + telemetry — **NOT** enable |
| E-9 | **완료** | External sign-off prep (package, checklist, evidence index) — **NOT** `ExternalReviewCompleted` |
| E-10 | **완료** | Enable Decision Gate adjudication — **Production Enable NO-GO** (`ProductionEnableAuthorized=false`) — **NOT** writer implementation |
| E-11 | **완료** | Anchor harness closure (candidate) |
| E-11.1 | **완료** | Anchor sign-off — B-1 **PARTIAL**; trusted monotonic **NOT IMPLEMENTED** |
| E-12 | **완료** | XChaCha24 harness closure package |
| E-12.1 | **완료** | Crypto sign-off — B-2 **APPROVED CANDIDATE**; `XChaCha24Implemented=false` |
| Blocker closure | **대기** | E-14.1 disk durability sign-off, live external witness, Phase H migration, service/UI wiring (E-14 review package **완료**; `ActualDiskDurabilityReviewed=false`) |
| Production enable | **금지** | **NOT AUTHORIZED** until explicit future GO record + blockers cleared + human sign-off |
| F | 대기 | Import/export → av3 object/segment |
| G | 대기 | Sentinel stub → Policy + Crypto Broker |
| H | 대기 | spd-vault → av3 migration wizard |
| I | 대기 | UI 상태·위저드·Recovery Center |
| J | 대기 | Fuzz, audit, release gate |

**즉시 적용 (A):** 원본 기본 유지, UI **아스트라 금고**, 레거시 백엔드 유지 + AVLT 병행.