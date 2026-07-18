# E-13 Full Vault Rollback Closure Matrix

**Coverage:** harness + stub external witness (av3-e13- temp only)

## Matrix outcomes

| Scenario | Expected posture |
|----------|------------------|
| External counter == vault generation | Pass (fresh) |
| External counter > vault generation | RollbackSuspected |
| External counter < vault generation | Stale witness |
| Digest mismatch | RollbackSuspected |
| Signature invalid | Rejected |
| Replay detected | Rejected |
| Server unavailable | NO production enable |
| Offline mode | No writer promotion |
| Machine binding mismatch | Recovery required |
| Full vault rollback + external witness current | Detected |
| Same-disk + vault rolled back together | **Not CLOSED** |
| Header commit OK, trusted anchor commit fail | No promotion |
| Trusted anchor OK, header commit fail | Recovery required |
| Cancellation during trusted commit | No promotion |
| Cleanup failure after prepare | No promotion |
| Concurrent same-root update | Denied |
| Independent roots | No interference |

## B-1 closure rule

- **CLOSED** requires external/hybrid trusted monotonic witness verification.
- Same-disk local anchor alone **cannot** justify CLOSED.

**Harness matrix:** **CLOSED** (E-13 `Av3PhaseE13Tests` + 3× stability).  
**Production coverage:** **PARTIAL** (no live external witness — E-13.1 sign-off confirms).  
**Same-disk only:** cannot justify **CLOSED**.

**Full Vault Rollback Coverage (E-13.1):** harness **CLOSED** · production **PARTIAL** · B-1 **PARTIAL / SIGNED CANDIDATE**.