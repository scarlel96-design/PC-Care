# E-14 Disk Durability Threat Model

**Phase:** E-14 — candidate only; **NOT** production enable  
**Date:** 2026-07-08

## Mandatory distinctions

- **harness durability closed** ≠ **production disk durability closed**
- Flush success does **not** always guarantee physical media persistence (OS write cache, controller reorder)
- Removable / network / cloud-synced paths require explicit policy — **no production writer** without review
- Unknown filesystem: **fail-closed** or recovery-required posture

## Scenario matrix (20)

| # | Scenario | Harness posture | Production writer posture |
|---|----------|-----------------|---------------------------|
| 1 | Power loss during object write | Classified; no trusted promotion | NOT AUTHORIZED until FI + sign-off |
| 2 | Power loss during metadata.root write | Recovery-required classification | NOT AUTHORIZED |
| 3 | Power loss during journal write | Recovery-required classification | NOT AUTHORIZED |
| 4 | Power loss during 3-copy header commit | `PowerLossBeforeHeaderNoPromotion` | NOT AUTHORIZED |
| 5 | Power loss after flush before rename | Partial commit; cleanup/recovery | NOT AUTHORIZED |
| 6 | Power loss after rename before directory sync | `DirectorySyncUnsupported` classified | Review-required |
| 7 | Power loss after header quorum before activation revalidation | `PowerLossBeforeRevalidationRecoveryRequired` | Recovery-required |
| 8 | OS write cache not durable | Flush+reread harness only | No physical-media guarantee |
| 9 | Filesystem reorder | Harness documents limitation | NOT AUTHORIZED without platform review |
| 10 | Antivirus / file-lock / backup interference | File-lock retry then classify | `FileLockExhausted` → no commit |
| 11 | Out-of-space | `OutOfSpace` — not committed | Fail-closed |
| 12 | Access denied | `AccessDenied` — not committed | Fail-closed |
| 13 | Long path / unicode / case sensitivity | Harness uses canonical temp roots | Review-required for production paths |
| 14 | Removable drive surprise removal | `SurpriseRemovalRecoveryRequired` | No production writer |
| 15 | NTFS vs exFAT vs ReFS | NTFS candidate; ReFS review-required; exFAT restricted | Policy-gated |
| 16 | Network drive / cloud-sync folder | No production writer | Explicit deny |
| 17 | BitLocker / EFS / compression / dedup | Documented interaction risk | Review-required |
| 18 | Crash during cleanup | `CleanupFailureNoTrustedPromotion` | Recovery-required |
| 19 | Stale temp file recovery | Classified without trusted promotion | Recovery-required if mutation |
| 20 | Partial sector / torn write | Harness FI alignment with E-4 R1 | NOT AUTHORIZED |

## Gate statements

- Production Writer: **NOT AUTHORIZED**
- Writer Enable Readiness: **NO-GO**
- `ActualDiskDurabilityReviewed=false` (E-14.1 sign-off gate)
- S-Class: **NOT YET SATISFIED**
- spd-vault on-disk data: **unchanged**