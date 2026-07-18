# Sentinel AI Policy Contract

> AV3 금고는 **NOT PRODUCTION** — Sentinel 연동은 Phase G. spd-vault 레거시는 **BELOW S-CLASS**.

- AI ≠ root of trust. Keys/plaintext/password 접근 **금지**.
- 출력: `SentinelDecision` enum only (`VaultPolicyEngine` 최종 결정).
- 구현: `Policy/SentinelDecision.cs` (deterministic stub). Phase G에서 App 연동.