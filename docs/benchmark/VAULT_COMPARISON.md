# WO28 - Vault comparison

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

## Cryptomator

Cryptomator provides cloud-oriented file encryption through a virtual filesystem. Its configuration authenticates format and cipher selection with the vault key. This differs from PC-Care's import/export model. [Architecture](https://docs.cryptomator.org/security/architecture/).

The recovery key represents master-key material. Password replacement does not re-encrypt existing file contents. Historical key-file copies remain relevant to credential security. [Password and recovery key](https://docs.cryptomator.org/desktop/password-and-recovery-key/).

Vault Recovery reconstructs supported configuration files. It does not recreate missing encrypted content. [Recovery scope](https://docs.cryptomator.org/desktop/vault-recovery/).

Inspected source: `RecoveryKeyResetPasswordController.restorePassword` prepares replacement configuration, loads the recovered key, and initializes configuration before moving files. `RecoveryDirectory` uses a temporary directory. Its moves use `REPLACE_EXISTING`; this observation does not prove a multi-file atomic commit. [Controller](https://github.com/cryptomator/cryptomator/blob/cb74d2c2adbca5c50b46ab60a5e1b4ac7165940a/src/main/java/org/cryptomator/ui/recoverykey/RecoveryKeyResetPasswordController.java); [directory](https://github.com/cryptomator/cryptomator/blob/cb74d2c2adbca5c50b46ab60a5e1b4ac7165940a/src/main/java/org/cryptomator/common/recovery/RecoveryDirectory.java).

`BackupRestorer.restoreIfBackupPresent` selects a matching backup by modification time. It copies over the configuration path and logs I/O failures. These selected methods do not establish rollback resistance. PC-Care must retain authenticated generation selection. [Source](https://github.com/cryptomator/cryptomator/blob/cb74d2c2adbca5c50b46ab60a5e1b4ac7165940a/src/main/java/org/cryptomator/common/recovery/BackupRestorer.java).

## VeraCrypt

Non-system volumes have an embedded backup header. External backups provide another recovery path. Restoring an old header also restores its credential state. Header recovery is distinct from restoring file contents. [Header backup and restore](https://veracrypt.io/en/Program%20Menu.html).

VeraCrypt's security model excludes verifying the integrity or authenticity of encrypted or decrypted data. Its volume design is not an AEAD replacement for PC-Care. [Security model](https://veracrypt.io/en/Security%20Model.html). `src/Common/Volumes.c` was inspected for primary/backup header-area handling. [Pinned source](https://github.com/veracrypt/VeraCrypt/blob/21c4d8817bd746e320bfecead81d35978006fdc8/src/Common/Volumes.c).

## PC-Care assessment

| Feature | Actual PC-Care evidence | Classification | Next proof |
|---|---|---|---|
| Authenticated export | `LabVaultService.ExportEntry`, `VaultExportWriter.WriteNew` and streaming crypto | 부분 구현 | Retain full-file hash and interrupted-export tests. |
| Product format routing | `SecureVaultLabBackend.Create` creates Lab v5; `AuthenticatedFormat` reports authenticated format | 부분 구현 | Exercise the actual product UI in an isolated profile. |
| Legacy password change | `LabVaultService.Phase2.ChangePassword` blocks public v4/v5 mutation | 개선 필요 | Prove a separately approved transition before changing policy. |
| Recovery explanation | Existing recovery slots and trusted receipts are different from Cryptomator's stable recovery key | 개선 필요 | Explain code consumption, configuration recovery, content recovery and freshness separately. |
| External backup workflow | No general production authenticated backup/restore flow was established by inspected paths | 추가 근거 필요 | Audit all backup entry points before proposing one. |
| Mounted virtual drive | Not established by these PC-Care paths | 도입 가치 없음 | Defer a new driver/filesystem while recovery P0 remains open. |

No row rates PC-Care's cryptographic security above these products. Recovery semantics are not interchangeable. All AV3 production gates remain closed.
