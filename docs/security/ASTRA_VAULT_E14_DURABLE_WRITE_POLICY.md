# E-14 Durable Write Policy

**Phase:** E-14 — candidate only  
**Date:** 2026-07-08  
**Code:** `Av3DiskDurabilityPolicy`, `Av3DiskDurabilityProbe`, `Av3DiskDurabilityHarnessRunner`

## Policy items

| Item | E-14 value |
|------|------------|
| Supported filesystem (production) | NTFS candidate only — **not enabled** |
| Local fixed disk | Required for production discussion |
| Removable drive | Restricted — explicit policy required |
| Network / cloud-sync | **No production writer** |
| Directory fsync / equivalent | **Unsupported** — classified `DirectorySyncUnsupported` |
| Atomic rename | NTFS likely; harness verified |
| Write-through / flush | Harness flush+reread; no physical-media guarantee |
| File lock retry | Max 3 attempts (`FileLockRetryMaxAttempts`) |
| Free space threshold | 4 MiB minimum (`MinimumFreeBytesThreshold`) |
| Temp cleanup | Stale temp — no trusted promotion without recovery |
| Long path | Harness uses OS temp canonical roots |
| Path canonicalization | `Av3WriterAccessGate.TryNormalizeHarnessRoot` |
| AV interference | Retry then `FileLockExhausted` |
| User-facing warnings | Public codes only — no secret leak |

## Critical flags

- `HarnessDurabilityClosedIsNotProductionDiskClosed=true`
- `ProductionDiskDurabilityClosed=false`
- `ActualDiskDurabilityReviewed=false` (code gate; E-14.1 required for true)
- `ActualDiskDurabilityReviewCandidate=true`
- `ProductionDiskDurabilityRouteEnabled=false`

## Mandatory statements

- **harness durability closed** is **not** production disk durability closed
- Flush success does **not** guarantee physical media persistence
- Production Writer: **NOT AUTHORIZED**
- Writer Enable Readiness: **NO-GO**
- S-Class: **NOT YET SATISFIED**