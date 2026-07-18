# E-14 User Media Policy

**Phase:** E-14 — review only; **NOT** production enable  
**Date:** 2026-07-08  
**Code mirror:** `Av3UserMediaPolicy`, `Av3DiskDurabilityClassifier`

## Classification (Windows 11 priority)

| Media / path | Classification | Production writer |
|--------------|----------------|-------------------|
| NTFS local fixed disk | `NtfsFixedDiskCandidate` | **Denied** until E-14.1 + enable GO |
| ReFS | `ReFsReviewRequired` | **Denied** — review required |
| exFAT / FAT32 | `ExFatRestricted` | **Denied** |
| Removable | `RemovableRestricted` | **Denied** without explicit policy |
| SMB / network share | `NetworkPathNoProductionWriter` | **Denied** |
| OneDrive / Dropbox / Google Drive / iCloud / Box | `CloudSyncNoProductionWriter` | **Denied** |
| Unknown filesystem | `UnknownFailClosed` | **Denied** (fail-closed) |
| E-14 harness (`av3-e14-`) | `HarnessSyntheticOnly` | Harness only — **NOT** production |

## Policy constants

- `NtfsLocalFixedDiskCandidate=true` — candidate only, not enable
- `ReFsReviewRequired=true`
- `ExFatRemovableRestricted=true`
- `SmbNetworkPathNoProductionWriter=true`
- `CloudSyncFolderNoProductionWriter=true`
- `UnknownFilesystemFailClosed=true`

## Mandatory statements

- Production Writer: **NOT AUTHORIZED**
- Writer Enable Readiness: **NO-GO**
- `ActualDiskDurabilityReviewed=false` (mandatory until E-14.1)
- Removable/network/cloud-sync: no production enable without explicit signed policy
- legacy spd-vault: **BELOW S-CLASS** — unchanged on disk