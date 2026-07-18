# E-11 Anchor Failure Matrix

| Case | Expected posture |
|------|------------------|
| Genesis (no anchor) | Unavailable until first commit |
| Generation equal | AnchorFresh |
| Anchor gen > vault gen | AnchorRollbackSuspected |
| Anchor gen < vault gen | AnchorStale |
| Digest mismatch | AnchorMismatch |
| Update failure | Not committed; no auto-repair |
| Header commit failed | Bridge skip; anchor not updated |
| Cleanup failure | Not promoted |
| Cancellation | Abort pending |
| Concurrent same root | Denied (in-flight) |
| Independent roots | No interference |
| Public errors | `av3_anchor_*` only; no secrets |

Enforced by `Av3PhaseE11Tests` + stability ×3.