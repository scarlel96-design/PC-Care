# E-14 Durability Failure Matrix

**Phase:** E-14  
**Date:** 2026-07-08  
**Tests:** `Av3PhaseE14Tests`

## Harness scope

| Test | Expected |
|------|----------|
| `E14_DurabilityHarness_RequiresAv3E14TempRoot` | Non-prefixed temp rejected |
| `E14_DurabilityHarness_RejectsUserVaultPath` | User workspace rejected |
| `E14_DurabilityHarness_RejectsDocumentsDesktopDownloads` | Forbidden tokens rejected |

## Media / filesystem probes

| Test | Failure reason | Trusted promotion |
|------|----------------|-------------------|
| `E14_DurabilityPolicy_NtfsFixedDisk_Candidate` | None (harness) | false |
| `E14_DurabilityPolicy_RemovableDrive_Restricted` | `RemovableMediaWithoutPolicy` | false |
| `E14_DurabilityPolicy_NetworkShare_NoProductionWriter` | `NetworkPathNoProductionWriter` | false |
| `E14_DurabilityPolicy_CloudSyncPath_NoProductionWriter` | `CloudSyncPathNoProductionWriter` | false |
| `E14_DurabilityPolicy_UnknownFilesystem_FailClosed` | `UnknownFilesystemFailClosed` | false |

## Durable write exercises

| Test | Result | Trusted promotion |
|------|--------|-------------------|
| `E14_WriteFlushReread_Pass` | PASS | false |
| `E14_RenameReplaceSemantics_Pass` | PASS | false |
| `E14_DirectorySyncUnsupported_Classified` | Classified | false |
| `E14_FileLock_RetryThenClassify` | Exhausted after 3 | false |
| `E14_AccessDenied_NotCommitted` | Denied | false |
| `E14_OutOfSpace_NotCommitted` | Denied | false |
| `E14_SurpriseRemoval_RecoveryRequired` | Recovery | false |
| `E14_StaleTempFile_RecoveredOrClassifiedNoMutation` | Classified | false |
| `E14_PowerLossBeforeHeader_NoTrustedPromotion` | No promotion | false |
| `E14_PowerLossAfterHeaderBeforeRevalidation_RecoveryRequired` | Recovery | false |
| `E14_CleanupFailure_NoTrustedPromotion` | No promotion | false |

## Gate / leak / stability

| Test | Expected |
|------|----------|
| `E14_EnableFlagsRemainFalse` | All enable flags false |
| `E14_DiskDurabilityInvariant_StabilityRepeat` ×3 | Invariant PASS |
| `E14_PublicError_Redacted` | No secret paths |
| `E14_NoSecretLeak_ReportManifestTrace` | Journal scanner PASS |

## Verdict

Failure matrix **CLOSED** for E-14 harness scope. Production disk durability **OPEN** until E-14.1 sign-off. Production Writer: **NOT AUTHORIZED**.