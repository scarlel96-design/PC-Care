# WO-08 process identity

Status: PARTIAL.

Branch: `codex/pccare-performance-diagnostics-accuracy`.
Baseline: `620d8def4f9857dc4d33d24d0ff668e445669144`.
Validated source: `77e19b01f1baa579811bbd122b062abaaa460ba1`.
Source tree: `41d94d1917e3dc12cc770cc3a8109f95b26ff9cf`.
Final evidence HEAD resolves from `evidence/wo08/GITHUB_RESULT.txt` and the final publication receipt.
Windows CI: [run 38084989987](https://github.com/scarlel96-design/PC-Care/actions/runs/38084989987).
Phase 1 remains NO-GO.

## Findings and implementation

One session registry serves Process API and ETW collectors.
The internal key uses PID, verified creation time, and generation.
Each exported identifier uses a random session salt and SHA-256.
Raw PID and names remain outside exported lifetime records.
Initial API snapshots identify processes already running before ETW starts.
ETW start events use live API creation time when it precedes the event.
Otherwise, the event supplies a separate observed lifetime.
Stop events close only eligible lifetimes. Half-open intervals separate PID reuse.
Ambiguous overlapping lifetimes return Unknown.
Unknown lifetimes never receive an invented process identity.
The registry retains at most 4096 lifetimes. Overflow produces a collection error.
Synthetic reuse, overlap, delayed stop, and reverse timestamps passed.
Real PID reuse and ETW attribution error remain NOT TESTED.
Lost lifecycle events can leave an incomplete interval. Consumers must check session loss.

## Evidence and rollback

Read the matching files in `evidence/wo08/`.
Use `ROLLBACK.sh` only on an independent EventBuffer source fixture.
Use a reviewed Git revert for project source restoration.
No install-transaction rollback was exercised by this source test.

## Observed process correlation

The General CI capture exported 11 ETW process identifiers.
All 11 also appeared in Process API snapshots from the same collection.
This proves identifier correlation for observed records. It does not prove attribution accuracy.
Detailed mode correlated nine of ten ETW identifiers with polling snapshots.
Short-lived processes can finish between polling intervals. Missing matches require further review.
The Detailed capture recorded 10,323 unattributed events after resource-event loss.
Real PID reuse and contribution errors still require an independent labeled reference.
