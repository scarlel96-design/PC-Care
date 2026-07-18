# Astra Vault Architecture Audit

## 코드 위치

| 구성요소 | 경로 |
|----------|------|
| 레거시 금고 (WinUI) | `src/SmartPerformanceDoctor.App/Services/Security/SecureVault*.cs` |
| UI | `Views/SecureVaultCenterPage.*`, `ViewModels/SecureVaultViewModel.cs` |
| 테스트 | `tests/.../SecurityAttackSimulationTests.cs`, `SecureVaultKdfTests.cs` |
| **신규 v3 코어** | `src/SmartPerformanceDoctor.AstraVault/` |
| VaultGate (Linux) | `VaultGate/crates/vaultgate-crypto` (LUKS envelope, Argon2id) |

## 레거시 on-disk (`%LocalAppData%/SmartPerformanceDoctor/secure_vault/default`)

```
vault.svdb
key_envelope.bin
vault_manifest.json.enc
data/shard_*.blob
data/redundant/
metadata/rate_limit_state.bin
audit/vault_audit.log.enc
recovery/recovery_envelope.bin, recovery_hint.enc
```

잠금 시: manifest·audit·envelope는 암호화 blob. `data/*.blob`는 ciphertext이나 **파일명 패턴이 정보를 누출**.

## 신규 목표 on-disk (`AstraVaultPaths`)

```
vault.locator
vault.header (+ redundant copies via header region)
metadata/metadata.root.enc
indexes/index.enc
chunks/xx/<random>.blob
packs/pack-NNNNNN.avpack  (Concealed profile)
journal/journal-NNNNNN.avj
recovery/recovery.enc
logs/security-events.log
```

## 통합 전략

1. **Dual backend** — `LegacyVaultBackend` (기존) + `AstraV3VaultBackend` (신규)  
2. `LegacyVaultInventory`가 마커로 분기 (`spd-vault` vs `AVLT`)  
3. UI는 `IAstraVaultHost` 단일 진입점  
4. Production writer는 **av3만** 승인 (레거시는 읽기·migration 전용)

## 감사 결론

레거시는 “앱 기반 암호화 금고” 수준에는 근접하나, **내역.txt의 v3 secure container·transaction·Sentinel** 요구는 미충족. 대개편은 신규 라이브러리 중심이 맞고, 레거시는 단계적 퇴역.