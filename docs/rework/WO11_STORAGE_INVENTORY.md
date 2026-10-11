# WO-11 — Storage inventory

Work branch: `codex/pccare-evidence-based-system-care`. Baseline: `b1d38b86a54cd5c192784a3ae5e9a3c321df2782`.
Baseline tree: `81cb2e509575fb516ecc77328d310a5bc4c41983`.
Release decision: **NO-GO**. Overall work status: **PARTIAL**.
Validation environment: Windows NT 10.0.26200.0, 24 logical processors, .NET SDK 10.0.401.
See `evidence/wo11/VERIFICATION.txt` for commands, outputs, hashes, and source-copy rollback.
See `evidence/wo11/GITHUB_RESULT.txt` for the CI source and final publication commit.

## Collection contract

The scanner visits at most 512 top-level entries, with a five-second enumeration budget.
Cancellation is checked between entries.
Directories are excluded. Their descendant sizes remain unknown.
File data is not read by the inventory scanner.
The scanner reads logical length, modification time, attributes, ownership, and file identity through held handles.
Hardlinks and reparse points are rejected by the existing boundary.
Access errors produce incomplete results. Missing bytes are not estimated.
A sharing violation produces InUse. Shared-read access does not prove an application has stopped using the file.

## Actual Windows results

The synthetic inventory completed in 51.5986 ms.
It measured 8,391,371 logical bytes, including fixture and transaction metadata.
One old temporary candidate represented 2,048 bytes.
Three other entries were excluded.
CPU time was 62.5 ms. Process working set was 41,451,520 bytes.

The ordinary user-profile Temp metadata scan reached the 512-entry budget.
It returned 511 protected directory entries and one RequiresReview file.
Measured file length was 53 bytes. No SafeCandidate was returned.
Complete was false. ExcludedCount was 512.
Elapsed time was 69.1404 ms. CPU time was 62.5 ms.
Process working set was 31,109,120 bytes. Target file mutations were zero.
These results do not describe all Temp contents or all user caches.

A separate sandbox-process Temp scan returned four excluded directory rows.
That sandbox Temp root differs from the ordinary user-profile root.
Do not combine those results into one inventory.

## Storage meaning

DriveInfo.TotalSize reports volume capacity.
AvailableFreeSpace reports availability for the caller.
TotalFreeSpace reports total free space.
MeasuredLogicalBytes reports observed logical file lengths.
It does not report allocation size or reclaimable storage.
CandidateBytes includes only revalidated preview candidates.
Final deletion requires separate authority and remains test-only.

## Privacy

Serialized candidate data excludes root paths and filenames.
Public evidence retains anonymous IDs, sizes, classifications, and measurement times.
The local UI displays filenames so the user can review exact candidates.
No vault content or document tree was opened.
