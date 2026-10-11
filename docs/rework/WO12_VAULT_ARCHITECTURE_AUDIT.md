# WO-12 vault architecture audit

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

## Actual route

SecureVaultCenterPage uses SecureVaultViewModel. The ViewModel uses SecureVaultService.
AstraVaultHostService delegates to SecureVaultService. Its product name does not select the AV3 Writer.
SecureVaultService detects existing legacy manifests and Lab headers.
An existing legacy manifest takes precedence when both formats exist.
ProductFeatureFlags.EnableProductHost enables the Lab UI route at app startup.
CreateVault uses that route for an empty vault. LabVaultService.Create now creates AVLT5 / spd-vault-v5-lab.
The flag name VaultV4UiEnabled therefore does not prove that new files use v4.
Runtime FeatureGateService remains an additional product execution gate.
An unsigned production Release can still block SecureVault under the existing trust policy.

| Component | Storage / operation | Approval and effects |
|---|---|---|
| SecureVaultService legacy path | spd-vault; read and write | Existing route; synthetic tests only; default imports preserve source |
| SecureVaultLabBackend | SecurityLab v4/v5 adapter | Product host route exists; v5 create tested; this is not AV3 authorization |
| LabVaultService | AVLT5 header, encrypted metadata, packed objects | Real synthetic filesystem operations; crash coverage remains partial |
| AstraVault AV3 | Separate next-generation container | Production Writer, Journal Writer and Migration remain disabled |
| VaultRecovery | Lab read-only recovery tooling | Separate tool; current WO-12 CLI password flow was not exercised |

## Documentation conflicts

Older Av3PhaseGate comments say the UI always uses the legacy backend.
Current product-host routing also selects the Lab backend.
ProductFeatureFlags names and older comments refer to v4. The current create path emits v5.
Historical production-candidate labels do not replace current false AV3 approval fields.
No format migration or approval was inferred from these comments.

## Implemented corrections

Unauthenticated journal records no longer authorize encrypted-object deletion.
Authenticated metadata references can reconcile a transaction without deleting object bytes.
Unreferenced objects remain preserved. Pending review forces a read-only session.
Independent instances hold an exclusive session.lock until Lock or Dispose.
The boundary implementation is reused through a compile-time Lab namespace adapter.
The normal updater namespace and execution policy remain unchanged.
