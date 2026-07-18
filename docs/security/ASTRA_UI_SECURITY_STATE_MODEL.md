# UI Security State Model

- **레거시 spd-vault:** UI에 **BELOW S-CLASS** 표시 (`LegacyVaultLabels`).
- **AV3 target:** **NOT PRODUCTION / READ-ONLY VALIDATION** (`Av3PhaseGate`) — 완성된 S급 금고처럼 표시 **금지**.

`VaultSecurityState` (`Session/VaultSecurityState.cs`) ↔ 화면 표시 1:1 매핑 필수.

레거시 `SecureVaultState`는 `AstraVaultHostService.MapState`로 브리지.  
Commit/verify 전 “완료” 문구 금지 (내역.txt §10).