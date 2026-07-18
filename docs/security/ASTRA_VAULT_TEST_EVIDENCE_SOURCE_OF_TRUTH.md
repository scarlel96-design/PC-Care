# PC 케어 보안 금고 — Test Evidence Source of Truth (AV3)

**Purpose:** Single authoritative table for **latest verified** CI test evidence used in enable **discussion** (not production enable).  
**Machine-readable mirror:** `tests/SmartPerformanceDoctor.Tests/TestAssets/av3_external_review_test_evidence.json`  
**Enforced by:** `Av3PhaseE91Tests.E91_Documentation_CurrentEvidence_MatchesSourceOfTruth_NoStale444`  
**E-10:** Preflight re-run at Enable Decision Gate; includes `Av3NamedSignoffTests` + `Av3PhaseE10SignoffTests`. Enforced by `Av3PhaseE10SignoffTests.E10_Preflight_CurrentEvidenceSourceOfTruthMatchesActual`.

## Latest verified (enable decision evidence — use only this block)

| Evidence ID | Command | Configuration | Platform | Passed | Failed | Skipped | Phase | Verified (UTC) |
|-------------|---------|---------------|----------|--------|--------|---------|-------|----------------|
| **E-TEST-SOT** | `dotnet test tests/SmartPerformanceDoctor.Tests/SmartPerformanceDoctor.Tests.csproj -c Release -p:Platform=x64` | Release | x64 | **657** | 0 | 0 | E-14 | 2026-07-08 |
| **E-TEST-AV3-FILTER** | `dotnet test` (same project) `--filter` `FullyQualifiedName~Av3PhaseE9\|FullyQualifiedName~Av3PhaseGate\|FullyQualifiedName~Av3PhaseE8\|FullyQualifiedName~Av3PhaseE7\|FullyQualifiedName~DryRun\|FullyQualifiedName~Invariant\|FullyQualifiedName~Fault\|FullyQualifiedName~Cancellation\|FullyQualifiedName~Recovery\|FullyQualifiedName~Av3PhaseE91\|FullyQualifiedName~Av3NamedSignoff\|FullyQualifiedName~Av3PhaseE10\|FullyQualifiedName~Av3PhaseE11\|FullyQualifiedName~Av3PhaseE111\|FullyQualifiedName~Av3PhaseE12\|FullyQualifiedName~Av3PhaseE121\|FullyQualifiedName~Av3PhaseE13\|FullyQualifiedName~Av3PhaseE131\|FullyQualifiedName~Av3PhaseE14` | Release | x64 | **393** | 0 | 0 | E-14 | 2026-07-08 |

**E-14 preflight:** build Release x64 **PASS**; full suite **657/657**; AV3 filter **393/393**; `Av3PhaseE14` durability matrix ×3 stability **PASS**.

**Precondition:** `dotnet build SmartPerformanceDoctor.sln -c Release -p:Platform=x64` succeeds.

## Historical phase results (not enable evidence)

| Phase | Label | Suite | Passed | Note |
|-------|-------|-------|--------|------|
| E-9 prep | **historical** | Full Release x64 | 444 | Stale package figure before test suite composition changed |
| Formal review re-run | **historical** | Full Release x64 | 388 | Formal external review snapshot before E-9.1 tests; superseded by **455** |
| E-9 prep | **historical** | AV3 filter (Writer/DryRun/Invariant/Fault/Cancellation) | 134 | Different filter string than E-9.1 canonical filter; superseded by **191** |
| E-9 gap note | **historical** | Full | 438 | Mentioned in gap report before E-9.1 SOT alignment |
| E-11 | **historical** | Full Release x64 | 501 | Pre-E-11.1 sign-off tests; superseded by **517** |
| E-11 | **historical** | AV3 filter (pre-E11/E111 in filter) | 237 | Superseded by **253** (E-11.1 canonical filter) |
| E-13.1 | **historical** | Full Release x64 | 620 | Pre-E-14 disk durability tests; superseded by **657** |
| E-13.1 | **historical** | AV3 filter (pre-E14 in filter) | 356 | Superseded by **393** (E-14 canonical filter) |
| E-13 | **historical** | Full Release x64 | 599 | Pre-E-13.1 sign-off tests |
| E-13 | **historical** | AV3 filter (pre-E131 in filter) | 335 | Superseded by E-13.1 filter |
| E-12.1 | **historical** | Full Release x64 | 567 | Pre-E-13 trusted anchor provider tests |
| E-12.1 | **historical** | AV3 filter (pre-E13 in filter) | 303 | Superseded by E-13 filter |
| E-12 | **historical** | Full Release x64 | 545 | Pre-E-12.1 sign-off tests |
| E-12 | **historical** | AV3 filter (pre-E121 in filter) | 281 | Superseded by E-12.1 filter |
| E-11.1 | **historical** | Full Release x64 | 517 | Pre-E-12 XChaCha24 tests |
| E-11.1 | **historical** | AV3 filter (pre-E12 in filter) | 253 | Superseded by **281** (E-12 filter) |
| E-10 | **historical** | Full Release x64 | 477 | Pre-E-11 anchor tests |
| E-9.1 | **historical** | Full Release x64 | 455 | Pre-E-10 named/E10 test additions |
| E-9.1 | **historical** | AV3 filter (E-9.1 canonical) | 191 | Superseded by **213** (E-10 filter adds NamedSignoff/E10) |

**Rule:** Do not copy historical counts into “latest verified” tables. `444/444` and `134/134` must not appear in current evidence docs without an explicit **historical** label.

## M-01 root cause (summary)

| Item | Explanation |
|------|-------------|
| 444 vs 388 | E-9 package refresh recorded an older full-suite size; formal review re-ran `SmartPerformanceDoctor.Tests` Release x64 and observed **388** passing tests (suite composition / branch / included tests differ from the earlier 444 snapshot). |
| 134 vs 114 | Filter expression differed (`Writer` vs explicit `Av3Phase*` / `DryRun` / `Recovery` tokens), changing matched test count. |
| Stale documentation | Evidence index and review package copied E-9 prep numbers without rebinding to the formal review command line. |